using Microsoft.Data.SqlClient;
using TripsProject.Models.ViewModel;

namespace TripsProject.Data;

public class BookingRepository
{
    private readonly string _connectionString;

    public BookingRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TravelDb");
    }

    public List<BookingRowVM> GetAll(string? search = null)
    {
        var list = new List<BookingRowVM>();

        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        var sql = @"
SELECT
    b.BookingId,
    b.UserEmail,
    b.PackageId,
    p.Destination,
    b.RoomsBooked,
    b.TotalAmount,
    b.Currency,
    b.Status,
    b.CreatedAt,
    b.PaidAt
FROM Bookings b
JOIN TravelPackages p ON p.PackageId = b.PackageId
WHERE
    (@q IS NULL OR @q = '')
    OR b.UserEmail LIKE '%' + @q + '%'
    OR p.Destination LIKE '%' + @q + '%'
    OR CAST(b.BookingId AS NVARCHAR(20)) = @q
    OR CAST(b.PackageId AS NVARCHAR(20)) = @q
    OR b.Status LIKE '%' + @q + '%'
ORDER BY b.CreatedAt DESC;
";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@q", (object?)search ?? "");

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new BookingRowVM
            {
                BookingId = (int)r["BookingId"],
                UserEmail = r["UserEmail"].ToString(),
                PackageId = (int)r["PackageId"],
                Destination = r["Destination"].ToString(),
                RoomsBooked = (int)r["RoomsBooked"],
                TotalAmount = (decimal)r["TotalAmount"],
                Currency = r["Currency"].ToString(),
                Status = r["Status"].ToString(),
                CreatedAt = (DateTime)r["CreatedAt"],
                PaidAt = r["PaidAt"] == DBNull.Value ? null : (DateTime?)r["PaidAt"]
            });
        }

        return list;
    }
}