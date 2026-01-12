using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TripsProject.Models;
using TripsProject.Models.ViewModel;
using TripsProject.Services;

namespace TripsProject.Controllers
{
    public class PaymentController : Controller
    {
        private readonly EmailService _emailService;
        private readonly string _connectionString;
        private readonly IConfiguration _config;

        public PaymentController(IConfiguration configuration, EmailService emailService)
        {
            _config = configuration;
            _connectionString = configuration.GetConnectionString("TravelDb");
            _emailService = emailService;
        }

        // ========= Pages =========

        [HttpGet]
        public IActionResult Success(string orderId)
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "User");

            if (string.IsNullOrWhiteSpace(orderId))
                return RedirectToAction("Index", "Trips");

            string userEmail = User.Identity!.Name!;

            var order = GetPaidOrderForUser(orderId, userEmail);
            if (order == null)
                return RedirectToAction("Index", "Trips");

            return View(order);
        }

        public IActionResult Cancel()
        {
            ViewBag.Msg = "Payment was cancelled";
            return View();
        }

        [HttpGet]
        public IActionResult Checkout(int packageId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                TempData["AuthMessage"] = "To purchase a package you must register or login";
                return RedirectToAction("Login", "User");
            }

            var package = GetPackageForCheckout(packageId);
            if (package == null)
                return NotFound("Package not found");

            // UX: do not allow going to checkout if there is no stock
            if (package.Amount <= 0 || !package.IsAvailable)
            {
                TempData["Msg"] = "This package is not available (out of stock).";
                return RedirectToAction("Details", "Trips", new { id = packageId });
            }

            // for client timer (10 minutes)
            ViewBag.ExpireAt = DateTime.UtcNow.AddMinutes(10);

            return View(package);
        }

        // ========= PayPal API =========

        // 1) Create PayPal Order (server): reserve stock + create PendingPayment locally, then create PayPal order
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest req)
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized();

            var package = GetPackageForCheckout(req.PackageId);
            if (package == null)
                return NotFound("Package not found");

            if (package.Amount <= 0 || !package.IsAvailable)
                return BadRequest("Out of stock");

            string userEmail = User.Identity!.Name!;
            decimal total = package.Price;

            // 1) reserve stock + create PendingPayment order (transaction)
            int? localOrderId = ReserveAndCreatePendingOrder(userEmail, package.PackageId, total);
            if (localOrderId == null)
                return BadRequest("Out of stock");

            // 2) create PayPal order
            string token = await GetPayPalAccessToken();
            string baseUrl = _config["PayPal:BaseUrl"];

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                intent = "CAPTURE",
                purchase_units = new[]
                {
                    new
                    {
                        reference_id = $"LOCAL-{localOrderId}",
                        amount = new
                        {
                            currency_code = "ILS",
                            value = total.ToString("0.00")
                        }
                    }
                }
            };

            var res = await http.PostAsync(
                $"{baseUrl}/v2/checkout/orders",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            );

            // if PayPal order creation fails -> release by local order id
            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync();
                ReleaseReservationByLocalOrderId(localOrderId.Value);
                return BadRequest("Create PayPal order failed: " + err);
            }

            string json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            string payPalOrderId = doc.RootElement.GetProperty("id").GetString()!;

            // 3) save PayPalOrderId into local order
            SavePayPalOrderId(localOrderId.Value, payPalOrderId);

            return Ok(new { orderId = payPalOrderId });
        }

        // 2) Capture PayPal Order (server): after COMPLETED mark Paid, then best-effort email+PDF
        [HttpPost]
        public async Task<IActionResult> Capture([FromBody] CaptureRequest req)
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized();

            string token = await GetPayPalAccessToken();
            string baseUrl = _config["PayPal:BaseUrl"];

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await http.PostAsync(
                $"{baseUrl}/v2/checkout/orders/{req.OrderId}/capture",
                new StringContent("{}", Encoding.UTF8, "application/json")
            );

            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync();
                ReleaseReservationByPayPalOrderId(req.OrderId);
                return BadRequest("Capture failed: " + err);
            }

            string json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            string status = doc.RootElement.GetProperty("status").GetString()!;
            if (status != "COMPLETED")
            {
                ReleaseReservationByPayPalOrderId(req.OrderId);
                return BadRequest("Payment not completed");
            }

            // ✅ mark as paid (DB)
            MarkOrderPaid(req.OrderId);

            // ✅ Best-effort email (never fail payment flow if email sending fails)
            try
            {
                var invoice = GetInvoiceDataByPayPalOrderId(req.OrderId);
                if (invoice != null)
                {
                    QuestPDF.Settings.License = LicenseType.Community;

                    byte[] pdfBytes = BuildInvoicePdf(invoice);

                    string subject = $"Order Confirmation #{invoice.OrderId} - TripsProject";

                    string body = $@"
<div style='font-family:Arial;'>
  <h2>Thank you! Your order has been successfully completed ✅</h2>
  <p><b>Order Number:</b> #{invoice.OrderId}</p>
  <p><b>Destination:</b> {invoice.Destination} ({invoice.Country})</p>
  <p><b>Travel Dates:</b> {invoice.StartDate:dd/MM/yyyy} - {invoice.EndDate:dd/MM/yyyy}</p>
  <p><b>Total Paid:</b> ₪{invoice.TotalPrice:0.00}</p>
  <p><b>Payment Date:</b> {(invoice.PaidAt.HasValue ? invoice.PaidAt.Value.ToString("dd/MM/yyyy HH:mm") : "-")}</p>
  <hr/>
  <p>The PDF invoice is attached to this email for your records.</p>
  <br/>
  <p>Best regards,<br/><b>TripsProject Team</b></p>
</div>";

                    await _emailService.SendWithAttachmentAsync(
                        to: invoice.UserEmail,
                        subject: subject,
                        htmlBody: body,
                        attachmentBytes: pdfBytes,
                        attachmentFileName: $"Invoice_{invoice.OrderId}.pdf",
                        attachmentMimeType: "application/pdf"
                    );
                }
            }
            catch
            {
                // do nothing - best effort
            }

            return Ok(new { success = true, orderId = req.OrderId });
        }

        // 3) Client cancelled / timer expired -> release reservation
        [HttpPost]
        public IActionResult CancelReservation([FromBody] CaptureRequest req)
        {
            ReleaseReservationByPayPalOrderId(req.OrderId);
            return Ok();
        }

        // ========= Reservation + Orders DB logic =========

        private int? ReserveAndCreatePendingOrder(string email, int packageId, decimal totalPrice)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                // 1) reserve stock (Amount-- only if Amount>0)
                var cmdStock = new SqlCommand(@"
UPDATE TravelPackages
SET Amount = Amount - 1
WHERE PackageId = @PackageId AND Amount > 0;
", conn, tx);

                cmdStock.Parameters.AddWithValue("@PackageId", packageId);

                int updated = cmdStock.ExecuteNonQuery();
                if (updated == 0)
                {
                    tx.Rollback();
                    return null;
                }

                // 2) update availability
                var cmdAvail = new SqlCommand(@"
UPDATE TravelPackages
SET IsAvailable = CASE WHEN Amount <= 0 THEN 0 ELSE 1 END
WHERE PackageId = @PackageId;
", conn, tx);

                cmdAvail.Parameters.AddWithValue("@PackageId", packageId);
                cmdAvail.ExecuteNonQuery();

                // 3) create pending order (needs columns Status, PayPalOrderId, PaidAt in Orders table)
                var cmdOrder = new SqlCommand(@"
INSERT INTO Orders (UserEmail, PackageID, TotalPrice, Status)
OUTPUT INSERTED.OrderID
VALUES (@Email, @PackageID, @TotalPrice, 'PendingPayment');
", conn, tx);

                cmdOrder.Parameters.AddWithValue("@Email", email);
                cmdOrder.Parameters.AddWithValue("@PackageID", packageId);
                cmdOrder.Parameters.AddWithValue("@TotalPrice", totalPrice);

                int orderId = (int)cmdOrder.ExecuteScalar();

                tx.Commit();
                return orderId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private void SavePayPalOrderId(int localOrderId, string payPalOrderId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var cmd = new SqlCommand(@"
UPDATE Orders
SET PayPalOrderId = @PayPalOrderId
WHERE OrderID = @OrderID AND Status = 'PendingPayment';
", conn);

            cmd.Parameters.AddWithValue("@PayPalOrderId", payPalOrderId);
            cmd.Parameters.AddWithValue("@OrderID", localOrderId);

            cmd.ExecuteNonQuery();
        }

        private void MarkOrderPaid(string payPalOrderId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var cmd = new SqlCommand(@"
UPDATE Orders
SET Status = 'Paid',
    PaidAt = GETDATE()
WHERE PayPalOrderId = @PayPalOrderId AND Status = 'PendingPayment';
", conn);

            cmd.Parameters.AddWithValue("@PayPalOrderId", payPalOrderId);
            cmd.ExecuteNonQuery();
        }

        private void ReleaseReservationByPayPalOrderId(string payPalOrderId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                var cmdGet = new SqlCommand(@"
SELECT PackageID
FROM Orders
WHERE PayPalOrderId = @PayPalOrderId AND Status = 'PendingPayment';
", conn, tx);

                cmdGet.Parameters.AddWithValue("@PayPalOrderId", payPalOrderId);

                var pidObj = cmdGet.ExecuteScalar();
                if (pidObj == null)
                {
                    tx.Commit();
                    return;
                }

                int packageId = (int)pidObj;

                var cmdBack = new SqlCommand(@"
UPDATE TravelPackages
SET Amount = Amount + 1,
    IsAvailable = 1
WHERE PackageId = @PackageId;
", conn, tx);

                cmdBack.Parameters.AddWithValue("@PackageId", packageId);
                cmdBack.ExecuteNonQuery();

                var cmdCancel = new SqlCommand(@"
UPDATE Orders
SET Status = 'Cancelled'
WHERE PayPalOrderId = @PayPalOrderId AND Status = 'PendingPayment';
", conn, tx);

                cmdCancel.Parameters.AddWithValue("@PayPalOrderId", payPalOrderId);
                cmdCancel.ExecuteNonQuery();

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private void ReleaseReservationByLocalOrderId(int localOrderId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                var cmdGet = new SqlCommand(@"
SELECT PackageID
FROM Orders
WHERE OrderID = @OrderID AND Status = 'PendingPayment';
", conn, tx);

                cmdGet.Parameters.AddWithValue("@OrderID", localOrderId);

                var pidObj = cmdGet.ExecuteScalar();
                if (pidObj == null)
                {
                    tx.Commit();
                    return;
                }

                int packageId = (int)pidObj;

                var cmdBack = new SqlCommand(@"
UPDATE TravelPackages
SET Amount = Amount + 1,
    IsAvailable = 1
WHERE PackageId = @PackageId;
", conn, tx);

                cmdBack.Parameters.AddWithValue("@PackageId", packageId);
                cmdBack.ExecuteNonQuery();

                var cmdCancel = new SqlCommand(@"
UPDATE Orders
SET Status = 'Cancelled'
WHERE OrderID = @OrderID AND Status = 'PendingPayment';
", conn, tx);

                cmdCancel.Parameters.AddWithValue("@OrderID", localOrderId);
                cmdCancel.ExecuteNonQuery();

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ========= PayPal Auth =========

        private async Task<string> GetPayPalAccessToken()
        {
            string baseUrl = _config["PayPal:BaseUrl"];
            string clientId = _config["PayPal:ClientId"];
            string secret = _config["PayPal:Secret"];

            using var http = new HttpClient();
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

            var content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

            var res = await http.PostAsync($"{baseUrl}/v1/oauth2/token", content);
            res.EnsureSuccessStatusCode();

            string json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.GetProperty("access_token").GetString()!;
        }

        // ========= DB Helpers =========

        private TravelPackage? GetPackageForCheckout(int packageId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string sql = @"
SELECT PackageId, Destination, Country, Price, Amount, IsAvailable, StartDate, EndDate, PackageType, NumOfPeople, Description
FROM TravelPackages
WHERE PackageId = @PackageId;
";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PackageId", packageId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            return new TravelPackage
            {
                PackageId = (int)reader["PackageId"],
                Destination = reader["Destination"].ToString(),
                Country = reader["Country"].ToString(),
                Price = (decimal)reader["Price"],
                Amount = (int)reader["Amount"],
                IsAvailable = (bool)reader["IsAvailable"],
                StartDate = (DateTime)reader["StartDate"],
                EndDate = (DateTime)reader["EndDate"],
                PackageType = reader["PackageType"] == DBNull.Value ? null : reader["PackageType"].ToString(),
                NumOfPeople = (int)reader["NumOfPeople"],
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString()
            };
        }

        // ========= Expired pending cleanup (optional call from admin/service) =========

        public int CancelExpiredPendingOrders(int minutes = 10)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                var cmdGet = new SqlCommand(@"
SELECT OrderID, PackageID
FROM Orders
WHERE Status = 'PendingPayment'
  AND OrderDate < DATEADD(minute, -@Minutes, GETDATE());
", conn, tx);

                cmdGet.Parameters.AddWithValue("@Minutes", minutes);

                var expired = new List<(int orderId, int packageId)>();

                using (var r = cmdGet.ExecuteReader())
                {
                    while (r.Read())
                        expired.Add(((int)r["OrderID"], (int)r["PackageID"]));
                }

                if (expired.Count == 0)
                {
                    tx.Commit();
                    return 0;
                }

                foreach (var e in expired)
                {
                    var cmdBack = new SqlCommand(@"
UPDATE TravelPackages
SET Amount = Amount + 1,
    IsAvailable = 1
WHERE PackageId = @PackageId;
", conn, tx);

                    cmdBack.Parameters.AddWithValue("@PackageId", e.packageId);
                    cmdBack.ExecuteNonQuery();

                    var cmdCancel = new SqlCommand(@"
UPDATE Orders
SET Status = 'Cancelled'
WHERE OrderID = @OrderID AND Status = 'PendingPayment';
", conn, tx);

                    cmdCancel.Parameters.AddWithValue("@OrderID", e.orderId);
                    cmdCancel.ExecuteNonQuery();
                }

                tx.Commit();
                return expired.Count;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private OrderDetailsVM? GetPaidOrderForUser(string payPalOrderId, string userEmail)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var cmd = new SqlCommand(@"
SELECT o.OrderID, o.UserEmail, o.PackageID, o.TotalPrice, o.Status, o.PaidAt,
       p.Destination, p.Country
FROM Orders o
JOIN TravelPackages p ON p.PackageId = o.PackageID
WHERE o.PayPalOrderId = @PayPalOrderId
  AND o.UserEmail = @UserEmail
  AND o.Status = 'Paid';
", conn);

            cmd.Parameters.AddWithValue("@PayPalOrderId", payPalOrderId);
            cmd.Parameters.AddWithValue("@UserEmail", userEmail);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new OrderDetailsVM
            {
                OrderID = (int)r["OrderID"],
                UserEmail = r["UserEmail"].ToString()!,
                PackageID = (int)r["PackageID"],
                TotalPrice = (decimal)r["TotalPrice"],
                Status = r["Status"].ToString()!,
                PaidAt = r["PaidAt"] == DBNull.Value ? null : (DateTime?)r["PaidAt"],
                Destination = r["Destination"].ToString()!,
                Country = r["Country"].ToString()!
            };
        }

        // ========= Invoice data + PDF =========

        private InvoiceData? GetInvoiceDataByPayPalOrderId(string payPalOrderId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var sql = @"
SELECT 
    o.OrderID,
    o.UserEmail,
    o.OrderDate,
    o.TotalPrice,
    o.Status,
    o.PaidAt,
    o.PayPalOrderId,
    p.PackageId,
    p.Destination,
    p.Country,
    p.StartDate,
    p.EndDate,
    p.PackageType,
    p.NumOfPeople,
    p.Description
FROM Orders o
JOIN TravelPackages p ON p.PackageId = o.PackageID
WHERE o.PayPalOrderId = @PayPalOrderId;
";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PayPalOrderId", payPalOrderId);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new InvoiceData
            {
                OrderId = (int)r["OrderID"],
                UserEmail = r["UserEmail"].ToString()!,
                OrderDate = (DateTime)r["OrderDate"],
                TotalPrice = (decimal)r["TotalPrice"],
                Status = r["Status"].ToString()!,
                PaidAt = r["PaidAt"] == DBNull.Value ? null : (DateTime?)r["PaidAt"],
                PayPalOrderId = r["PayPalOrderId"] == DBNull.Value ? null : r["PayPalOrderId"].ToString(),

                PackageId = (int)r["PackageId"],
                Destination = r["Destination"].ToString()!,
                Country = r["Country"].ToString()!,
                StartDate = (DateTime)r["StartDate"],
                EndDate = (DateTime)r["EndDate"],
                PackageType = r["PackageType"] == DBNull.Value ? null : r["PackageType"].ToString(),
                NumOfPeople = (int)r["NumOfPeople"],
                Description = r["Description"] == DBNull.Value ? null : r["Description"].ToString(),
            };
        }

        private byte[] BuildInvoicePdf(InvoiceData data)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Text("TripsProject - Invoice").FontSize(18).SemiBold();
                        row.ConstantItem(200).AlignRight().Text($"Invoice #{data.OrderId}");
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        col.Item().LineHorizontal(1);

                        col.Item().Text($"Customer Email: {data.UserEmail}");
                        col.Item().Text($"Order Date: {data.OrderDate:yyyy-MM-dd HH:mm}");
                        col.Item().Text($"Paid At: {(data.PaidAt.HasValue ? data.PaidAt.Value.ToString("yyyy-MM-dd HH:mm") : "-")}");
                        col.Item().Text($"Status: {data.Status}");
                        col.Item().Text($"PayPal Order Id: {data.PayPalOrderId ?? "-"}");

                        col.Item().LineHorizontal(1);

                        col.Item().Text("Package Details").FontSize(14).SemiBold();

                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn();
                                c.RelativeColumn();
                            });

                            static IContainer Cell(IContainer x) =>
                                x.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6);

                            void Row(string k, string v)
                            {
                                t.Cell().Element(Cell).Text(k).SemiBold();
                                t.Cell().Element(Cell).Text(v);
                            }

                            Row("Destination", $"{data.Destination} ({data.Country})");
                            Row("Dates", $"{data.StartDate:dd/MM/yyyy} - {data.EndDate:dd/MM/yyyy}");
                            Row("Package Type", data.PackageType ?? "-");
                            Row("People", data.NumOfPeople.ToString());
                            Row("Description", data.Description ?? "-");
                        });

                        col.Item().LineHorizontal(1);

                        col.Item().AlignRight()
                            .Text($"Total: ₪{data.TotalPrice:0.00}")
                            .FontSize(16).SemiBold();
                    });

                    page.Footer().AlignCenter().Text("© TripsProject").FontSize(10);
                });
            }).GeneratePdf();
        }

        private class InvoiceData
        {
            public int OrderId { get; set; }
            public string UserEmail { get; set; } = "";
            public DateTime OrderDate { get; set; }
            public decimal TotalPrice { get; set; }
            public string Status { get; set; } = "";
            public DateTime? PaidAt { get; set; }
            public string? PayPalOrderId { get; set; }

            public int PackageId { get; set; }
            public string Destination { get; set; } = "";
            public string Country { get; set; } = "";
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string? PackageType { get; set; }
            public int NumOfPeople { get; set; }
            public string? Description { get; set; }
        }
    }

    // ========= DTOs =========
    public class CreateOrderRequest
    {
        public int PackageId { get; set; }
    }

    public class CaptureRequest
    {
        public string OrderId { get; set; } = "";
    }
}
