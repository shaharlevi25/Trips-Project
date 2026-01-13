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
        // GET /Trips/Details/{id}
[HttpGet("Details/{id}")]
public IActionResult Details(int id)
{
    TravelPackage? pkg = null;

    using var conn = new SqlConnection(_connectionString);
    conn.Open();

    // 1) שליפת החבילה
    using (var cmd = new SqlCommand(@"
        SELECT PackageId, Destination, Country, StartDate, EndDate,
               Price, NumOfPeople, PackageType, AgeLimit,
               Description, CreatedAt, IsAvailable, Amount
        FROM TravelPackages
        WHERE PackageId = @id;
    ", conn))
    {
        cmd.Parameters.AddWithValue("@id", id);

        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return NotFound();

        pkg = new TravelPackage
        {
            PackageId = r.GetInt32(0),
            Destination = r.IsDBNull(1) ? "" : r.GetString(1),
            Country = r.IsDBNull(2) ? "" : r.GetString(2),
            StartDate = r.GetDateTime(3),
            EndDate = r.GetDateTime(4),
            Price = r.GetDecimal(5),
            NumOfPeople = r.GetInt32(6),
            PackageType = r.IsDBNull(7) ? "" : r.GetString(7),
            AgeLimit = r.IsDBNull(8) ? null : r.GetInt32(8),
            Description = r.IsDBNull(9) ? "" : r.GetString(9),
            CreatedAt = r.GetDateTime(10),
            IsAvailable = r.GetBoolean(11),
            Amount = r.IsDBNull(12) ? 0 : r.GetInt32(12)
        };
    }

    // 2) שליפת הנחה פעילה להיום (כמו ב-Index רק לחבילה אחת)
    var today = DateTime.Today;

    using (var discountCmd = new SqlCommand(@"
        SELECT TOP 1 DiscountPercent
        FROM PackageDiscounts
        WHERE PackageID = @pid
          AND StartDate <= @today
          AND EndDate >= @today
        ORDER BY DiscountPercent DESC, DiscountID DESC;
    ", conn))
    {
        discountCmd.Parameters.AddWithValue("@pid", pkg.PackageId);
        discountCmd.Parameters.AddWithValue("@today", today);

        var obj = discountCmd.ExecuteScalar();
        if (obj != null && obj != DBNull.Value)
        {
            int percent = (int)obj;
            pkg.DiscountPercent = percent;
            pkg.DiscountedPrice = Math.Round(pkg.Price * (100m - percent) / 100m, 2);
        }
    }

    return View(pkg);
}

    }
}