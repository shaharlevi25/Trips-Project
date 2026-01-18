using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TripsProject.Data;
using TripsProject.Services;

namespace TripsProject.Controllers
{
    public class UserOrdersController : Controller
    {
        private readonly OrderRepository _repo;
        private readonly string _cs;
        private readonly EmailService _email;
        private readonly PackageRepository _package;

        public UserOrdersController(OrderRepository repo,IConfiguration config, EmailService email,PackageRepository pack)
        {
            _repo = repo;
            _cs = config.GetConnectionString("TravelDb");
            _email = email;
            _package = pack;
        }
        public IActionResult CancelNotAllowed()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int orderId)
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized();

            string email = User.Identity!.Name!;

            var result = _repo.CancelPaidOrderByUser(orderId, email);
            if (!result.Success)
            {
                TempData["CancelTitle"] = "Cancellation Not Allowed";
                TempData["CancelMsg"] = result.Error ?? "You cannot cancel this order.";
                return RedirectToAction("CancelNotAllowed");

            }

            if (result.WasAmountZeroBeforeCancel)
            {
                try
                {
                    var waitlistEmails = _repo.GetWaitlistEmailsForPackage(result.PackageId);

                    if (waitlistEmails.Count > 0)
                    {
                        var package = _package.GetPackageById(result.PackageId);
                        string subject = "A spot just opened up for a travel package!";
                        string body = $@"
<div style='font-family:Arial; line-height:1.6; color:#1f2937'>
  <h2 style='margin:0 0 10px;'>Good news ✅</h2>
  <p style='margin:0 0 14px;'>A spot has just opened for a package you are waiting for.</p>

  <table style='border-collapse:collapse; width:100%; max-width:650px;'>
    <tr>
      <td style='padding:8px; border:1px solid #e5e7eb; width:180px;'><b>Package</b></td>
      <td style='padding:8px; border:1px solid #e5e7eb;'>
        {(package != null
            ? $"{package.Destination}, {package.Country} | {package.StartDate:dd/MM/yyyy} - {package.EndDate:dd/MM/yyyy} | {package.PackageType}"
            : "N/A")}
      </td>
    </tr>

    <tr>
      <td style='padding:8px; border:1px solid #e5e7eb;'><b>Destination</b></td>
      <td style='padding:8px; border:1px solid #e5e7eb;'>
        {(package != null ? $"{package.Destination}, {package.Country}" : "N/A")}
      </td>
    </tr>

    <tr>
      <td style='padding:8px; border:1px solid #e5e7eb;'><b>Dates</b></td>
      <td style='padding:8px; border:1px solid #e5e7eb;'>
        {(package != null ? $"{package.StartDate:dd/MM/yyyy} - {package.EndDate:dd/MM/yyyy}" : "N/A")}
      </td>
    </tr>

    <tr>
      <td style='padding:8px; border:1px solid #e5e7eb;'><b>Price</b></td>
      <td style='padding:8px; border:1px solid #e5e7eb;'>
        {(package != null ? package.Price.ToString("0.00") + " USD" : "N/A")}
      </td>
    </tr>
  </table>

  <p style='margin:16px 0 10px;'>
    ⏳ Availability is limited — please log in and book as soon as possible.
  </p>

  <hr style='border:none; border-top:1px solid #e5e7eb; margin:18px 0;' />

  <p style='margin:0;'>TripsProject Team</p>
</div>
";

                        foreach (var to in waitlistEmails)
                        {
                            try
                            {
                                await _email.SendAsync(to, subject, body);
                            }
                            catch { /* ignore per-recipient */ }
                        }

                        _repo.MarkWaitlistNotified(result.PackageId);
                    }
                }
                catch
                {
                    // best effort:
                }
            }

            return RedirectToAction("MyOrders");
        }

        [HttpGet]
        public IActionResult MyOrders()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "User");

            string email = User.Identity!.Name!;
            var orders = _repo.GetUserOrders(email);

            return View(orders);
        }

        [HttpGet]
        public IActionResult MyOrdersSearch(string? query)
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized();

            string email = User.Identity!.Name!;
            var orders = _repo.GetUserOrders(email, query);

            return PartialView("_MyOrdersTable", orders);
        }
        
         public IActionResult InvoicePdf(int orderId)
        {
            if (!User.Identity!.IsAuthenticated)
                return RedirectToAction("Login", "User");

            string email = User.Identity!.Name!;

            var data = GetInvoiceData(orderId, email);
            if (data == null)
                return NotFound("Order not found");

           
            if (!string.Equals(data.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Invoice is available only for paid orders.");

            QuestPDF.Settings.License = LicenseType.Community;

            byte[] pdf = Document.Create(container =>
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
                        col.Spacing(12);

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

                            void Row(string k, string v)
                            {
                                t.Cell().Element(CellStyle).Text(k).SemiBold();
                                t.Cell().Element(CellStyle).Text(v);
                            }

                            static IContainer CellStyle(IContainer x) =>
                                x.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6);

                            Row("Destination", $"{data.Destination} ({data.Country})");
                            Row("Dates", $"{data.StartDate:dd/MM/yyyy} - {data.EndDate:dd/MM/yyyy}");
                            Row("Package Type", data.PackageType ?? "-");
                            Row("People", data.NumOfPeople.ToString());
                            Row("Description", data.Description ?? "-");
                        });

                        col.Item().LineHorizontal(1);

                        col.Item().AlignRight().Text($"Total: ₪{data.TotalPrice:0.00}")
                            .FontSize(16).SemiBold();
                    });

                    page.Footer().AlignCenter().Text("© TripsProject").FontSize(10);
                });
                
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Text("TripsProject - Trip Itinerary").FontSize(18).SemiBold();
                        row.ConstantItem(200).AlignRight().Text($"Order #{data.OrderId}");
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(12);

                        col.Item().LineHorizontal(1);

                        col.Item().Text("Travel Route / Track").FontSize(14).SemiBold();

                        col.Item().Text(string.IsNullOrWhiteSpace(data.TrackDesc)
                            ? "No itinerary/track description was provided for this package."
                            : data.TrackDesc);

                        col.Item().LineHorizontal(1);

                        col.Item().Text("Notes").FontSize(14).SemiBold();
                        col.Item().Text("Please arrive on time to each activity. The schedule may change due to weather or local conditions.");
                    });

                    page.Footer().AlignCenter().Text("© TripsProject").FontSize(10);
                });
            }).GeneratePdf();

            return File(pdf, "application/pdf", $"Invoice_{data.OrderId}.pdf");
        }

        private InvoiceData? GetInvoiceData(int orderId, string email)
        {
            using var conn = new SqlConnection(_cs);
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
    p.Description,
    p.TrackDesc
FROM Orders o
JOIN TravelPackages p ON p.PackageId = o.PackageID
WHERE o.OrderID = @OrderID AND o.UserEmail = @Email;
";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@OrderID", orderId);
            cmd.Parameters.AddWithValue("@Email", email);

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
                PackageType = r["PackageType"]?.ToString(),
                NumOfPeople = (int)r["NumOfPeople"],
                Description = r["Description"]?.ToString(),
                TrackDesc = r["TrackDesc"] == DBNull.Value ? null : r["TrackDesc"].ToString()
            };
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
            public string? TrackDesc { get; set; }
        }
    }
    
}