using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TripsProject.Models;

namespace TripsProject.Controllers
{
    public class PaymentController : Controller
    {
        private readonly string _connectionString;
        private readonly IConfiguration _config;

        public PaymentController(IConfiguration configuration)
        {
            _config = configuration;
            _connectionString = configuration.GetConnectionString("TravelDb");
        }

        // ========= Pages =========

        public IActionResult Success()
        {
            ViewBag.Msg = "התשלום בוצע בהצלחה (Sandbox) וההזמנה נשמרה!";
            return View();
        }

        public IActionResult Cancel()
        {
            ViewBag.Msg = "התשלום בוטל";
            return View();
        }

        [HttpGet]
        public IActionResult Checkout(int packageId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                TempData["AuthMessage"] = "כדי להזמין חבילה יש להירשם או להתחבר למערכת";
                return RedirectToAction("Login", "User");
            }

            var package = GetPackageForCheckout(packageId);
            if (package == null)
                return NotFound("Package not found");

            // UX: אם אין מלאי, אל תאפשר בכלל להגיע לתשלום
            if (package.Amount <= 0 || !package.IsAvailable)
            {
                TempData["Msg"] = "החבילה אינה זמינה להזמנה (אזל המלאי).";
                return RedirectToAction("Details", "Trips", new { id = packageId });
            }
            ViewBag.ExpireAt = DateTime.UtcNow.AddMinutes(10);

            return View(package);
        }

        // ========= PayPal API =========

        // 1) Create PayPal Order (שרת) - קודם Reserve מלאי + יצירת Pending Order, ואז PayPal
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest req)
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized();

            var package = GetPackageForCheckout(req.PackageId);
            if (package == null)
                return NotFound("Package not found");

            // הגנה כפולה
            if (package.Amount <= 0 || !package.IsAvailable)
                return BadRequest("החבילה אזלה מהמלאי");

            string userEmail = User.Identity!.Name!;
            decimal total = package.Price;

            // 1) Reserve + יצירת הזמנה PendingPayment (Transaction)
            int? localOrderId = ReserveAndCreatePendingOrder(userEmail, package.PackageId, total);
            if (localOrderId == null)
                return BadRequest("החבילה אזלה מהמלאי");

            // 2) יצירת PayPal order
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
                        // קשר בין PayPal להזמנה שלנו
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

            // אם נכשל ביצירת PayPal order -> לשחרר רזרבה ולהפוך הזמנה ל-Cancelled
            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync();

                // ✅ פה אין עדיין PayPalOrderId, אז משחררים לפי ה-localOrderId
                ReleaseReservationByLocalOrderId(localOrderId.Value);

                return BadRequest("Create PayPal order failed: " + err);
            }

            string json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            string payPalOrderId = doc.RootElement.GetProperty("id").GetString()!;

            // 3) שמירת PayPalOrderId להזמנה המקומית
            SavePayPalOrderId(localOrderId.Value, payPalOrderId);

            return Ok(new { orderId = payPalOrderId });
        }

        // 2) Capture PayPal Order (שרת) - אחרי COMPLETED מסמנים Paid
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

            string status = doc.RootElement.GetProperty("status").GetString()!; // COMPLETED
            if (status != "COMPLETED")
            {
                ReleaseReservationByPayPalOrderId(req.OrderId);
                return BadRequest("Payment not completed");
            }

            // התשלום הצליח -> מעדכנים הזמנה ל-Paid
            MarkOrderPaid(req.OrderId);

            return Ok(new { success = true });
        }

        // 3) ביטול מצד הלקוח - מחזיר מלאי ומסמן Cancelled
        [HttpPost]
        public IActionResult CancelReservation([FromBody] CaptureRequest req)
        {
            // לא חובה להיות מחובר כדי לשחרר, אבל עדיף כן:
            // אם אתה רוצה לחייב התחברות, תפתח:
            // if (!User.Identity.IsAuthenticated) return Unauthorized();

            ReleaseReservationByPayPalOrderId(req.OrderId);
            return Ok();
        }

        // ========= Reservation + Orders DB logic =========

        // Reserve מלאי + יצירת הזמנה PendingPayment
        private int? ReserveAndCreatePendingOrder(string email, int packageId, decimal totalPrice)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                // 1) Reserve Stock (Amount-- אם Amount>0)
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

                // 2) עדכון זמינות
                var cmdAvail = new SqlCommand(@"
                    UPDATE TravelPackages
                    SET IsAvailable = CASE WHEN Amount <= 0 THEN 0 ELSE 1 END
                    WHERE PackageId = @PackageId;
                ", conn, tx);

                cmdAvail.Parameters.AddWithValue("@PackageId", packageId);
                cmdAvail.ExecuteNonQuery();

                // 3) יצירת הזמנה PendingPayment
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

        // שחרור רזרבה לפי PayPalOrderId (cancel / failure)
        private void ReleaseReservationByPayPalOrderId(string payPalOrderId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                // מוצאים PackageID להזמנה שעדיין Pending
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

                // מחזירים מלאי ומעדכנים זמינות
                var cmdBack = new SqlCommand(@"
                    UPDATE TravelPackages
                    SET Amount = Amount + 1,
                        IsAvailable = 1
                    WHERE PackageId = @PackageId;
                ", conn, tx);

                cmdBack.Parameters.AddWithValue("@PackageId", packageId);
                cmdBack.ExecuteNonQuery();

                // מסמנים הזמנה כ-Cancelled
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

        // שחרור רזרבה לפי OrderID (אם PayPal Create נכשל)
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

            var content = new StringContent(
                "grant_type=client_credentials",
                Encoding.UTF8,
                "application/x-www-form-urlencoded"
            );

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

            string sql = @"SELECT PackageId, Destination, Country, Price, Amount, IsAvailable
                           FROM TravelPackages
                           WHERE PackageId = @PackageId";

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
                IsAvailable = (bool)reader["IsAvailable"]
            };
        }
        public int CancelExpiredPendingOrders(int minutes = 10)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                // 1) למצוא הזמנות שפגו
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

                // 2) להחזיר מלאי
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

        
    }
    

    // ========= DTOs =========
    public class CreateOrderRequest
    {
        public int PackageId { get; set; }
    }

    public class CaptureRequest
    {
        public string OrderId { get; set; }
    }
}
