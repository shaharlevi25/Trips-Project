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
}