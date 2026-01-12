using Microsoft.Data.SqlClient;
using TripsProject.Models.ViewModel;

namespace TripsProject.Data;

public class OrderRepository
{
    private readonly string _connectionString;

    public OrderRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TravelDb");
    }

    public List<OrderRowVM> GetAll(string? search = null)
    {
        var list = new List<OrderRowVM>();

        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        var sql = @"
SELECT
    o.OrderID,
    o.UserEmail,
    o.PackageID,
    p.Destination,
    o.TotalPrice,
    o.Status,
    o.OrderDate,
    o.PaidAt,
    o.PayPalOrderId
FROM Orders o
JOIN TravelPackages p ON p.PackageId = o.PackageID
WHERE
    (@q IS NULL OR @q = '')
    OR o.UserEmail LIKE '%' + @q + '%'
    OR p.Destination LIKE '%' + @q + '%'
    OR CAST(o.OrderID AS NVARCHAR(20)) = @q
    OR CAST(o.PackageID AS NVARCHAR(20)) = @q
    OR o.Status LIKE '%' + @q + '%'
    OR o.PayPalOrderId LIKE '%' + @q + '%'
ORDER BY o.OrderDate DESC;
";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@q", (object?)search ?? "");

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new OrderRowVM
            {
                OrderId = (int)r["OrderID"],
                UserEmail = r["UserEmail"].ToString()!,
                PackageId = (int)r["PackageID"],
                Destination = r["Destination"].ToString()!,
                TotalPrice = (decimal)r["TotalPrice"],
                Status = r["Status"].ToString()!,
                OrderDate = (DateTime)r["OrderDate"],
                PaidAt = r["PaidAt"] == DBNull.Value ? null : (DateTime?)r["PaidAt"],
                PayPalOrderId = r["PayPalOrderId"] == DBNull.Value ? null : r["PayPalOrderId"].ToString()
            });
        }

        return list;
        }
}