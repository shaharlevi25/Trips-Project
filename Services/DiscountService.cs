using Microsoft.Data.SqlClient;

namespace TripsProject.Services
{
    public class DiscountService
    {
        private readonly string _connectionString;

        public DiscountService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("TravelDb");
        }

        public int? GetActiveDiscountPercent(int packageId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var cmd = new SqlCommand(@"
SELECT TOP 1 DiscountPercent
FROM PackageDiscounts
WHERE PackageID = @PackageId
  AND CAST(GETDATE() AS date) BETWEEN StartDate AND EndDate
ORDER BY DiscountPercent DESC, DiscountID DESC;
", conn);

            cmd.Parameters.AddWithValue("@PackageId", packageId);

            var obj = cmd.ExecuteScalar();
            if (obj == null || obj == DBNull.Value) return null;

            return (int)obj;
        }

        public decimal ApplyDiscount(decimal price, int discountPercent)
        {
            if (discountPercent <= 0) return price;
            if (discountPercent > 100) discountPercent = 100;

            return Math.Round(price * (1 - discountPercent / 100m), 2);
        }
    }
}