using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TripsProject.Models;

namespace TripsProject.Controllers
{
    [Route("Trips")]
    public class TripsController : Controller
    {
        
        private readonly string _connectionString;

        public TripsController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("TravelDb")!;
        }

        // GET /Travels
[HttpGet("")]
public IActionResult Index(
    string? destination,
    DateTime? start,
    DateTime? end,
    int adults = 2,
    string? packageType = null,
    bool searched = false,
    int children = 0
    
    
)
{
    var packages = new List<TravelPackage>();

    using var conn = new SqlConnection(_connectionString);
    conn.Open();

    // בסיס השאילתה
    string sql = @"
        SELECT PackageId, Destination, Country, StartDate, EndDate,
               Price, NumOfPeople, PackageType, AgeLimit,
               Description, CreatedAt, IsAvailable
        FROM TravelPackages
        WHERE 1=1
    ";

    using var cmd = new SqlCommand();
    cmd.Connection = conn;
    if (searched)
    {
        // יעד/עיר (Destination) - LIKE
        if (!string.IsNullOrWhiteSpace(destination))
        {
            sql += @"
                    AND (
                        Destination LIKE '%' + @destination + '%'
                        OR Country LIKE '%' + @destination + '%'
                    )";

            cmd.Parameters.AddWithValue("@destination", destination.Trim());
        }

        // טווח תאריכים - חפיפה בין הטווח המבוקש לחבילה
        // start: החבילה חייבת להסתיים אחרי start
        if (start.HasValue)
        {
            sql += " AND EndDate >= @start";
            cmd.Parameters.AddWithValue("@start", start.Value.Date);
        }

        // end: החבילה חייבת להתחיל לפני end
        if (end.HasValue)
        {
            sql += " AND StartDate <= @end";
            cmd.Parameters.AddWithValue("@end", end.Value.Date);
        }


        int totalPeople = Math.Max(0, adults) + Math.Max(0, children);
        if (totalPeople > 0)
        {
            sql += " AND NumOfPeople = @totalPeople";
            cmd.Parameters.AddWithValue("@totalPeople", totalPeople);
        }



        if (!string.IsNullOrEmpty(packageType))
        {
            sql += " AND PackageType = @PackageType";
            cmd.Parameters.AddWithValue("@PackageType", packageType);
        }

    }

    sql += " ORDER BY CreatedAt DESC";
    cmd.CommandText = sql;

    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var pkg = new TravelPackage
        {
            PackageId   = reader.GetInt32(0),
            Destination = reader.IsDBNull(1) ? "" : reader.GetString(1),
            Country     = reader.IsDBNull(2) ? "" : reader.GetString(2),
            StartDate   = reader.GetDateTime(3),
            EndDate     = reader.GetDateTime(4),
            Price       = reader.GetDecimal(5),
            NumOfPeople = reader.GetInt32(6),
            PackageType = reader.IsDBNull(7) ? "" : reader.GetString(7),
            AgeLimit    = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            Description = reader.IsDBNull(9) ? "" : reader.GetString(9),
            CreatedAt   = reader.GetDateTime(10),
            IsAvailable = reader.GetBoolean(11)
        };

        packages.Add(pkg);
    }

    if (searched)
    {
        ViewBag.Destination = destination ?? "";
        ViewBag.Start = start?.ToString("yyyy-MM-dd") ?? "";
        ViewBag.End = end?.ToString("yyyy-MM-dd") ?? "";
        ViewBag.PackageType = packageType;
        ViewBag.Adults = adults;
        ViewBag.Children = children;
    }


    return View(packages);
}


    }
}
