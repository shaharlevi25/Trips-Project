using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TripsProject.Models;
using Microsoft.Extensions.Configuration;
using System;

namespace TripsProject.Controllers
{
    public class PaymentController : Controller
    {
        private readonly string _connectionString;

        public PaymentController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("TravelDb");
        }

        public IActionResult Success()
        {
            ViewBag.Msg = "התשלום בוצע בהצלחה (Sandbox)";
            return View();
        }

        public IActionResult Cancel()
        {
            ViewBag.Msg = "התשלום בוטל";
            return View();
        }

        public IActionResult Checkout(int packageId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                TempData["AuthMessage"] = "כדי להזמין חבילה יש להירשם או להתחבר למערכת";
                return RedirectToAction("Register", "User");
            }

            TravelPackage package = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string sql = "SELECT * FROM TravelPackages WHERE PackageId = @PackageId";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@PackageId", packageId);

                    // ✅ סוגרים reader לפני שאילתת ההנחה
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            package = new TravelPackage
                            {
                                PackageId = (int)reader["PackageId"],
                                Destination = reader["Destination"].ToString(),
                                Country = reader["Country"].ToString(),
                                Price = (decimal)reader["Price"]
                            };
                        }
                    }
                }

                if (package != null)
                {
                    // ✅ שליפת הנחה פעילה (היום) + חישוב מחיר מוזל
                    var today = DateTime.Today;

                    string discountSql = @"
                        SELECT TOP 1 DiscountPercent
                        FROM PackageDiscounts
                        WHERE PackageID = @PackageId
                          AND StartDate <= @today
                          AND EndDate >= @today
                        ORDER BY DiscountPercent DESC";

                    using (SqlCommand discountCmd = new SqlCommand(discountSql, conn))
                    {
                        discountCmd.Parameters.AddWithValue("@PackageId", packageId);
                        discountCmd.Parameters.AddWithValue("@today", today);

                        object result = discountCmd.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            int percent = Convert.ToInt32(result);
                            package.DiscountPercent = percent;
                            package.DiscountedPrice = Math.Round(package.Price * (100m - percent) / 100m, 2);
                        }
                    }
                }
            }

            if (package == null)
                return NotFound("Package not found");

            return View(package);
        }
    }
}
