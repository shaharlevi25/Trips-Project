using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TripsProject.Models;
using Microsoft.Extensions.Configuration;

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
            TravelPackage package = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string sql = "SELECT * FROM TravelPackages WHERE PackageId = @PackageId";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@PackageId", packageId);

                SqlDataReader reader = cmd.ExecuteReader();
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

            if (package == null)
                return NotFound("Package not found");

            return View(package);
        }
    }
}