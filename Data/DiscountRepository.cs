using Microsoft.Data.SqlClient;
using TripsProject.Models.ViewModel;

namespace TripsProject.Data;

public class DiscountRepository
{
    private readonly string _connectionString;

    public DiscountRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TravelDb");
    }

    public List<DiscountRowVM> GetAll(string search = "")
    {
        var list = new List<DiscountRowVM>();

        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        var sql = @"
SELECT
    d.DiscountID,
    d.PackageID,
    p.Destination,
    p.Price AS OriginalPrice,
    d.DiscountPercent,
    d.StartDate,
    d.EndDate
FROM PackageDiscounts d
JOIN TravelPackages p ON p.PackageId = d.PackageID
WHERE (@s = '' 
       OR p.Destination LIKE '%' + @s + '%'
       OR CAST(d.PackageID AS NVARCHAR(20)) LIKE '%' + @s + '%')
ORDER BY d.DiscountID DESC;
";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@s", search ?? "");

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new DiscountRowVM
            {
                DiscountID = (int)r["DiscountID"],
                PackageID = (int)r["PackageID"],
                Destination = r["Destination"].ToString(),
                OriginalPrice = (decimal)r["OriginalPrice"],
                DiscountPercent = (int)r["DiscountPercent"],
                StartDate = (DateTime)r["StartDate"],
                EndDate = (DateTime)r["EndDate"]
            });
        }

        return list;
    }
    
    public List<DiscountVM> GetPackagesForPicker(string search = "")
        {
            var list = new List<DiscountVM>();

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var sql = @"
SELECT PackageId, Destination, Country, StartDate, EndDate, Price
FROM TravelPackages
WHERE (@s = '' 
       OR Destination LIKE '%' + @s + '%'
       OR CAST(PackageId AS NVARCHAR(20)) LIKE '%' + @s + '%')
ORDER BY PackageId DESC;
";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@s", search ?? "");

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new DiscountVM
                {
                    PackageID = (int)r["PackageId"],
                    Destination = r["Destination"].ToString(),
                    Country = r["Country"].ToString(),
                    PackageStart = (DateTime)r["StartDate"],
                    PackageEnd = (DateTime)r["EndDate"],
                    OriginalPrice = (decimal)r["Price"]
                });
            }

            return list;
        }

        // Load one package info for Create form
        public DiscountVM GetPackageInfo(int packageId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var cmd = new SqlCommand(@"
SELECT PackageId, Destination, Country, StartDate, EndDate, Price
FROM TravelPackages
WHERE PackageId = @id;
", conn);

            cmd.Parameters.AddWithValue("@id", packageId);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new DiscountVM
            {
                PackageID = (int)r["PackageId"],
                Destination = r["Destination"].ToString(),
                Country = r["Country"].ToString(),
                PackageStart = (DateTime)r["StartDate"],
                PackageEnd = (DateTime)r["EndDate"],
                OriginalPrice = (decimal)r["Price"]
            };
        }

        // Only one active discount at a time (active TODAY)
        public bool HasActiveDiscountNow(int packageId, int? excludeDiscountId = null)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var sql = @"
SELECT COUNT(*)
FROM PackageDiscounts
WHERE PackageID = @pid
  AND CAST(GETDATE() AS date) BETWEEN StartDate AND EndDate
";

            if (excludeDiscountId.HasValue)
                sql += " AND DiscountID <> @did";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@pid", packageId);

            if (excludeDiscountId.HasValue)
                cmd.Parameters.AddWithValue("@did", excludeDiscountId.Value);

            return (int)cmd.ExecuteScalar() > 0;
        }

        public void AddDiscount(DiscountVM vm)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var cmd = new SqlCommand(@"
INSERT INTO PackageDiscounts (PackageID, DiscountPercent, StartDate, EndDate)
VALUES (@pid, @percent, @start, @end);
", conn);

            cmd.Parameters.AddWithValue("@pid", vm.PackageID);
            cmd.Parameters.AddWithValue("@percent", vm.DiscountPercent);
            cmd.Parameters.AddWithValue("@start", vm.StartDate.Date);
            cmd.Parameters.AddWithValue("@end", vm.EndDate.Date);

            cmd.ExecuteNonQuery();
        }
        
        public DiscountVM GetDiscountForEdit(int discountId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var cmd = new SqlCommand(@"
SELECT d.DiscountID, d.PackageID, d.DiscountPercent, d.StartDate, d.EndDate,
       p.Destination, p.Country, p.Price AS OriginalPrice, p.StartDate AS PackageStart, p.EndDate AS PackageEnd
FROM PackageDiscounts d
JOIN TravelPackages p ON p.PackageId = d.PackageID
WHERE d.DiscountID = @id;
", conn);

            cmd.Parameters.AddWithValue("@id", discountId);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new DiscountVM
            {
                DiscountID = (int)r["DiscountID"],
                PackageID = (int)r["PackageID"],
                DiscountPercent = (int)r["DiscountPercent"],
                StartDate = (DateTime)r["StartDate"],
                EndDate = (DateTime)r["EndDate"],

                Destination = r["Destination"].ToString(),
                Country = r["Country"].ToString(),
                OriginalPrice = (decimal)r["OriginalPrice"],
                PackageStart = (DateTime)r["PackageStart"],
                PackageEnd = (DateTime)r["PackageEnd"]
            };
        }

        public void UpdateDiscount(DiscountVM vm)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var cmd = new SqlCommand(@"
UPDATE PackageDiscounts
SET DiscountPercent = @percent,
    StartDate = @start,
    EndDate = @end
WHERE DiscountID = @id;
", conn);

            cmd.Parameters.AddWithValue("@id", vm.DiscountID);
            cmd.Parameters.AddWithValue("@percent", vm.DiscountPercent);
            cmd.Parameters.AddWithValue("@start", vm.StartDate.Date);
            cmd.Parameters.AddWithValue("@end", vm.EndDate.Date);

            cmd.ExecuteNonQuery();
        }
        public bool HasOverlap(int packageId, DateTime start, DateTime end, int? excludeDiscountId = null)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var sql = @"
SELECT COUNT(*)
FROM PackageDiscounts
WHERE PackageID = @pid
  AND StartDate <= @newEnd
  AND EndDate >= @newStart
";
            if (excludeDiscountId.HasValue)
                sql += " AND DiscountID <> @did";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@pid", packageId);
            cmd.Parameters.AddWithValue("@newStart", start.Date);
            cmd.Parameters.AddWithValue("@newEnd", end.Date);

            if (excludeDiscountId.HasValue)
                cmd.Parameters.AddWithValue("@did", excludeDiscountId.Value);

            return (int)cmd.ExecuteScalar() > 0;
        }
        public void DeleteDiscount(int discountId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(
                "DELETE FROM PackageDiscounts WHERE DiscountID = @id", conn);

            cmd.Parameters.AddWithValue("@id", discountId);
            cmd.ExecuteNonQuery();
        }

    
    
}