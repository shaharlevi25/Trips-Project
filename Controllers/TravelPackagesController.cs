using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using TripsProject.Models;
using System;

namespace TripsProject.Controllers
{
    public class TravelPackagesController : Controller
    {
        private readonly string _connectionString;

        public TravelPackagesController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("TravelDb");
        }

        // GET: /TravelPackages/Details/1
        public IActionResult Details(int id)
        {
            TravelPackage package = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string sql = "SELECT * FROM TravelPackages WHERE PackageId = @PackageId";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@PackageId", id);

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
                            StartDate = (DateTime)reader["StartDate"],
                            EndDate = (DateTime)reader["EndDate"],
                            Price = (decimal)reader["Price"],
                            NumOfPeople = (int)reader["NumOfPeople"],
                            PackageType = reader["PackageType"].ToString(),
                            AgeLimit = reader["AgeLimit"] == DBNull.Value ? null : (int?)reader["AgeLimit"],
                            Description = reader["Description"].ToString(),
                            IsAvailable = (bool)reader["IsAvailable"],
                            Amount = (int)reader["Amount"]
                        };
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
                        discountCmd.Parameters.AddWithValue("@PackageId", id);
                        discountCmd.Parameters.AddWithValue("@today", today);

                        object result = discountCmd.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            int percent = Convert.ToInt32(result);

                            package.DiscountPercent = percent;
                            package.DiscountedPrice =
                                Math.Round(package.Price * (100m - percent) / 100m, 2);
                        }
                    }
                }
            }

            if (package == null)
                return NotFound();

            return View(package);
        }
    }
}
