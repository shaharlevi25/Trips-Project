using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TripsProject.Data;
using TripsProject.Models;

namespace TripsProject.Controllers
{
    [Route("Trips")]
    public class TripsController : Controller
    {
        private readonly string _connectionString;
        private readonly PackageRepository _repo;

        public TripsController(IConfiguration config, PackageRepository  packageRepository)
        {
            _connectionString = config.GetConnectionString("TravelDb")!;
            _repo = packageRepository;
        }

        // GET /Trips
        [HttpGet("")]
        public IActionResult Index(
            string? q,
            string? destination,
            DateTime? start,
            DateTime? end,
            int adults = 2,
            string? packageType = null,
            string? sort = null,
            decimal? maxPrice = null,
            bool discountOnly = false,
            bool searched = false,
            int children = 0
        )
        {
            var packages = new List<TravelPackage>();

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            // ===== Stats: total packages + sum amount =====
            using (var statsCmd = new SqlCommand(@"
                SELECT COUNT(*) AS TotalTrips,
                       ISNULL(SUM(Amount), 0) AS TotalAmount
                FROM TravelPackages;", conn))
            {
                using var statsReader = statsCmd.ExecuteReader();
                if (statsReader.Read())
                {
                    ViewBag.TotalTrips = statsReader.GetInt32(0);
                    ViewBag.TotalAmount = statsReader.GetInt32(1);
                }
            }

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
                // Free text search (q): "Paris honeymoon package"
                if (!string.IsNullOrWhiteSpace(q))
                {
                    ViewBag.Q = q;

                    var tokens = q.Trim()
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    int ti = 0;
                    foreach (var token in tokens)
                    {
                        var paramName = $"@q{ti}";
                        sql += $@"
                        AND (
                            Destination LIKE '%' + {paramName} + '%'
                            OR Country LIKE '%' + {paramName} + '%'
                            OR PackageType LIKE '%' + {paramName} + '%'
                            OR Description LIKE '%' + {paramName} + '%'
                        )";
                                    cmd.Parameters.AddWithValue(paramName, token);
                        ti++;
                    }
                }
                else
                {
                    ViewBag.Q = "";
                }

                // טווח תאריכים - חפיפה בין הטווח המבוקש לחבילה
                if (start.HasValue)
                {
                    sql += " AND EndDate >= @start";
                    cmd.Parameters.AddWithValue("@start", start.Value.Date);
                }

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

            // ✅ לסגור Reader לפני שאילתה נוספת
            {
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var pkg = new TravelPackage
                    {
                        PackageId = reader.GetInt32(0),
                        Destination = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Country = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        StartDate = reader.GetDateTime(3),
                        EndDate = reader.GetDateTime(4),
                        Price = reader.GetDecimal(5),
                        NumOfPeople = reader.GetInt32(6),
                        PackageType = reader.IsDBNull(7) ? "" : reader.GetString(7),
                        AgeLimit = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                        Description = reader.IsDBNull(9) ? "" : reader.GetString(9),
                        CreatedAt = reader.GetDateTime(10),
                        IsAvailable = reader.GetBoolean(11)
                    };

                    packages.Add(pkg);
                }
            }

            // ===== שליפת הנחות אחרי שמילאת packages =====
            if (packages.Count > 0)
            {
                var today = DateTime.Today;

                var ids = packages.Select(p => p.PackageId).Distinct().ToList();
                var inParams = ids.Select((id, i) => $"@id{i}").ToList();

                var discountSql = $@"
                    SELECT PackageID, DiscountPercent
                    FROM PackageDiscounts
                    WHERE PackageID IN ({string.Join(",", inParams)})
                      AND StartDate <= @today
                      AND EndDate >= @today
                ";

                using var discountCmd = new SqlCommand(discountSql, conn);
                discountCmd.Parameters.AddWithValue("@today", today);

                for (int i = 0; i < ids.Count; i++)
                    discountCmd.Parameters.AddWithValue(inParams[i], ids[i]);

                var bestDiscount = new Dictionary<int, int>();

                {
                    using var dr = discountCmd.ExecuteReader();
                    while (dr.Read())
                    {
                        int packageId = dr.GetInt32(0);
                        int percent = dr.GetInt32(1);

                        if (!bestDiscount.ContainsKey(packageId) || percent > bestDiscount[packageId])
                            bestDiscount[packageId] = percent;
                    }
                }

                foreach (var p in packages)
                {
                    if (bestDiscount.TryGetValue(p.PackageId, out var percent))
                    {
                        p.DiscountPercent = percent;
                        p.DiscountedPrice = Math.Round(p.Price * (100m - percent) / 100m, 2);
                    }
                }
            }

            // ===== Extra filters/sorting (only when searching) =====
            if (searched)
            {
                IEnumerable<TravelPackage> result = packages;

                if (discountOnly)
                    result = result.Where(p => p.DiscountedPrice != null);

                if (maxPrice.HasValue && maxPrice.Value > 0)
                    result = result.Where(p => (p.DiscountedPrice ?? p.Price) <= maxPrice.Value);

                if (!string.IsNullOrWhiteSpace(sort))
                {
                    switch (sort)
                    {
                        case "price_asc":
                            result = result.OrderBy(p => (p.DiscountedPrice ?? p.Price));
                            break;
                        case "price_desc":
                            result = result.OrderByDescending(p => (p.DiscountedPrice ?? p.Price));
                            break;
                    }
                }

                packages = result.ToList();
            }

            if (searched)
            {
                ViewBag.Destination = destination ?? "";
                ViewBag.Start = start?.ToString("yyyy-MM-dd") ?? "";
                ViewBag.End = end?.ToString("yyyy-MM-dd") ?? "";
                ViewBag.PackageType = packageType;
                ViewBag.Adults = adults;
                ViewBag.Children = children;
                ViewBag.Q = q ?? "";
                ViewBag.Sort = sort ?? "";
                ViewBag.MaxPrice = maxPrice?.ToString() ?? "";
                ViewBag.DiscountOnly = discountOnly;
            }
            _repo.ExpireOldPackages();

            return View(packages);
        }
        
        
    }
}