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
        private bool CreateOrderAndUpdateStock(string email, int packageId, decimal totalPrice)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var tx = conn.BeginTransaction();

            try
            {
                // 1) מורידים מלאי רק אם יש
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
                    return false; // אין מלאי
                }

                // 2) אם Amount הגיע ל-0 -> IsAvailable = 0
                var cmdAvail = new SqlCommand(@"
            UPDATE TravelPackages
            SET IsAvailable = CASE WHEN Amount <= 0 THEN 0 ELSE 1 END
            WHERE PackageId = @PackageId;
        ", conn, tx);

                cmdAvail.Parameters.AddWithValue("@PackageId", packageId);
                cmdAvail.ExecuteNonQuery();

                // 3) הוספת הזמנה
                var cmdOrder = new SqlCommand(@"
            INSERT INTO Orders (UserEmail, PackageID, TotalPrice)
            VALUES (@Email, @PackageID, @TotalPrice);
        ", conn, tx);

                cmdOrder.Parameters.AddWithValue("@Email", email);
                cmdOrder.Parameters.AddWithValue("@PackageID", packageId);
                cmdOrder.Parameters.AddWithValue("@TotalPrice", totalPrice);

                cmdOrder.ExecuteNonQuery();

                tx.Commit();
                return true;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
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

            return View(package);
        }

        // ========= PayPal API =========

        // 1) Create PayPal Order (שרת!)
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest req)
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized();

            // מחיר נשלף רק מה-DB (לא מהלקוח!)
            var package = GetPackageForCheckout(req.PackageId);
            if (package == null)
                return NotFound("Package not found");

            decimal total = package.Price;

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
                        reference_id = $"PKG-{package.PackageId}",
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

            res.EnsureSuccessStatusCode();

            string json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            string orderId = doc.RootElement.GetProperty("id").GetString()!;
            return Ok(new { orderId });
        }

        // 2) Capture PayPal Order (שרת!) + INSERT Orders
        [HttpPost]
        public async Task<IActionResult> Capture([FromBody] CaptureRequest req)
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized();

            string token = await GetPayPalAccessToken();
            string baseUrl = _config["PayPal:BaseUrl"];

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await http.PostAsync($"{baseUrl}/v2/checkout/orders/{req.OrderId}/capture", null);
            res.EnsureSuccessStatusCode();

            string json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            string status = doc.RootElement.GetProperty("status").GetString()!; // COMPLETED
            if (status != "COMPLETED")
                return BadRequest("Payment not completed");

            // reference_id = "PKG-12"
            string referenceId = doc.RootElement
                .GetProperty("purchase_units")[0]
                .GetProperty("reference_id")
                .GetString()!;

            int packageId = int.Parse(referenceId.Replace("PKG-", ""));

            // שוב מחיר מה-DB
            var package = GetPackageForCheckout(packageId);
            if (package == null)
                return BadRequest("Package not found");

            string userEmail = User.Identity!.Name!; // Email מה-Claim

            bool ok = CreateOrderAndUpdateStock(userEmail, package.PackageId, package.Price);

            if (!ok)
                return BadRequest("החבילה אזלה מהמלאי");

            return Ok(new { success = true });
        }

        // ========= Helpers =========

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

        private TravelPackage? GetPackageForCheckout(int packageId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string sql = @"SELECT PackageId, Destination, Country, Price
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
                Price = (decimal)reader["Price"]
            };
        }

        private void InsertOrder(string email, int packageId, decimal totalPrice)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string sql = @"INSERT INTO Orders (UserEmail, PackageID, TotalPrice)
                           VALUES (@Email, @PackageID, @TotalPrice)";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@PackageID", packageId);
            cmd.Parameters.AddWithValue("@TotalPrice", totalPrice);

            cmd.ExecuteNonQuery();
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
