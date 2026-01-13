using Microsoft.Data.SqlClient;
using TripsProject.Models.ViewModel;

namespace TripsProject.Data;

public class WaitingListRepository
{
    private readonly string _connectionString;

    public WaitingListRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TravelDb");
    }

    public List<WaitingListRowVM> GetAll(string? search = null)
    {
        var list = new List<WaitingListRowVM>();

        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        // Search by: destination / packageId / userId / status
        var sql = @"
SELECT 
    w.WaitingID,
    w.UserID,
    w.PackageID,
    p.Destination,
    w.RequestDate,
    w.Status
FROM WaitingList w
LEFT JOIN TravelPackages p ON p.PackageId = w.PackageID
WHERE
    (@q IS NULL OR @q = '')
    OR w.UserID LIKE '%' + @q + '%'
    OR p.Destination LIKE '%' + @q + '%'
    OR w.Status LIKE '%' + @q + '%'
    OR CAST(w.PackageID AS NVARCHAR(20)) = @q
ORDER BY 
    CASE WHEN w.Status = 'Waiting' THEN 0 ELSE 1 END,
    w.RequestDate DESC,
    w.WaitingID DESC;
";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@q", (object?)search ?? "");

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new WaitingListRowVM
            {
                WaitingID = (int)r["WaitingID"],
                UserID = r["UserID"].ToString(),
                PackageID = r["PackageID"] == DBNull.Value ? 0 : (int)r["PackageID"],
                Destination = r["Destination"] == DBNull.Value ? "-" : r["Destination"].ToString(),
                RequestDate = r["RequestDate"] == DBNull.Value ? null : (DateTime?)r["RequestDate"],
                Status = r["Status"] == DBNull.Value ? "Waiting" : r["Status"].ToString()
            });
        }

        return list;
    }
    public List<MyWaitlistRowVM> GetMyWaitlist(string userEmail, string? search = null)
    {
        var list = new List<MyWaitlistRowVM>();

        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        var sql = @"
SELECT
    w.WaitingID,
    w.PackageID,
    w.RequestDate,
    w.Status,
    p.Destination,
    p.Country,
    p.StartDate,
    p.EndDate
FROM WaitingList w
JOIN TravelPackages p ON p.PackageId = w.PackageID
WHERE
    w.UserID = @email
    AND (
        (@q IS NULL OR @q = '')
        OR p.Destination LIKE '%' + @q + '%'
        OR p.Country LIKE '%' + @q + '%'
        OR w.Status LIKE '%' + @q + '%'
        OR CAST(w.WaitingID AS NVARCHAR(20)) = @q
        OR CAST(w.PackageID AS NVARCHAR(20)) = @q
    )
ORDER BY w.RequestDate DESC;
";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@email", userEmail);
        cmd.Parameters.AddWithValue("@q", (object?)search ?? "");

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new MyWaitlistRowVM
            {
                WaitingId = (int)r["WaitingID"],
                PackageId = (int)r["PackageID"],
                RequestDate = (DateTime)r["RequestDate"],
                Status = r["Status"]?.ToString() ?? "Pending",
                Destination = r["Destination"].ToString()!,
                Country = r["Country"].ToString()!,
                StartDate = (DateTime)r["StartDate"],
                EndDate = (DateTime)r["EndDate"]
            });
        }

        return list;
    }
    public bool LeaveWaitlist(int waitingId, string userEmail)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        var sql = @"DELETE FROM WaitingList WHERE WaitingID = @id AND UserID = @email;";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", waitingId);
        cmd.Parameters.AddWithValue("@email", userEmail);

        return cmd.ExecuteNonQuery() > 0;
    }
    
}