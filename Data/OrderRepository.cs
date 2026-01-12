using Microsoft.Data.SqlClient;
using TripsProject.Models.ViewModel;

namespace TripsProject.Data;

public record CancelOrderResult(
    bool Success,
    string? Error,
    int PackageId,
    bool WasAmountZeroBeforeCancel,
    DateTime StartDate
);


public class OrderRepository
{
    private readonly string _connectionString;

    public OrderRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TravelDb");
    }
    public int GetCancellationDaysBeforeStart()
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        // לוקחים את החוקים האחרונים (אם יש כמה שורות)
        using var cmd = new SqlCommand(@"
                SELECT TOP 1 CancellationDaysBeforeStart
                FROM BookingRules
                ORDER BY RulesId DESC;
            ", conn);

        var obj = cmd.ExecuteScalar();
        if (obj == null) return 0; // ברירת מחדל אם לא הוגדר
        return (int)obj;
    }
    
    
    public CancelOrderResult CancelPaidOrderByUser(int orderId, string userEmail)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            int cancelDays = 0;

            // 1) rules
            using (var cmdRules = new SqlCommand(@"
                   SELECT TOP 1 CancellationDaysBeforeStart
                   FROM BookingRules
                   ORDER BY RulesId DESC;
               ", conn, tx))
            { var obj = cmdRules.ExecuteScalar(); cancelDays = obj == null ? 0 : (int)obj;
            }

                // 2) מביאים את ההזמנה + החבילה עם נעילה כדי למנוע race
                int packageId;
                string status;
                DateTime startDate;
                int amount;

                using (var cmdGet = new SqlCommand(@"
                SELECT o.OrderID, o.Status, o.PackageID, p.StartDate, p.Amount
                FROM Orders o WITH (UPDLOCK, ROWLOCK)
                JOIN TravelPackages p WITH (UPDLOCK, ROWLOCK) ON p.PackageId = o.PackageID
                WHERE o.OrderID = @OrderID AND o.UserEmail = @Email;
                ", conn, tx))
                {
                    cmdGet.Parameters.AddWithValue("@OrderID", orderId);
                    cmdGet.Parameters.AddWithValue("@Email", userEmail);

                    using var r = cmdGet.ExecuteReader();
                    if (!r.Read())
                    {
                        tx.Rollback();
                        return new CancelOrderResult(false, "Order not found for this user", 0, false, default);
                    }

                    packageId = (int)r["PackageID"];
                    status = r["Status"].ToString()!;
                    startDate = (DateTime)r["StartDate"];
                    amount = (int)r["Amount"];
                }

                // רק הזמנה ששולמה אפשר לבטל (אם אתה רוצה גם PendingPayment, תגיד)
                if (!string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase))
                {
                    tx.Rollback();
                    return new CancelOrderResult(false, "Only Paid orders can be cancelled", packageId, false, startDate);
                }

                // 3) חוק ביטול: צריך להיות לפני StartDate - CancellationDaysBeforeStart
                // לדוגמה: cancelDays=3 => אפשר לבטל עד 3 ימים לפני
                var deadline = startDate.AddDays(-cancelDays);
                if (DateTime.Now > deadline)
                {
                    tx.Rollback();
                    return new CancelOrderResult(false, $"Cancellation is allowed only until {deadline:dd/MM/yyyy HH:mm}", packageId, false, startDate);
                }

                bool wasZero = (amount == 0);

                // 4) עדכון ההזמנה ל-Cancelled
                using (var cmdCancel = new SqlCommand(@"
                    UPDATE Orders
                    SET Status = 'Cancelled',
                        CancelledAt = GETDATE()
                    WHERE OrderID = @OrderID
                      AND UserEmail = @Email
                      AND Status = 'Paid';
                ", conn, tx))
                {
                    cmdCancel.Parameters.AddWithValue("@OrderID", orderId);
                    cmdCancel.Parameters.AddWithValue("@Email", userEmail);

                    int rows = cmdCancel.ExecuteNonQuery();
                    if (rows == 0)
                    {
                        tx.Rollback();
                        return new CancelOrderResult(false, "Order could not be cancelled (status changed)", packageId, false, startDate);
                    }
                }

                // 5) מחזירים מלאי + זמינות
                using (var cmdBack = new SqlCommand(@"
                    UPDATE TravelPackages
                    SET Amount = Amount + 1,
                        IsAvailable = 1
                    WHERE PackageId = @PackageId;
                ", conn, tx))
                {
                    cmdBack.Parameters.AddWithValue("@PackageId", packageId);
                    cmdBack.ExecuteNonQuery();
                }

                tx.Commit();
                return new CancelOrderResult(true, null, packageId, wasZero, startDate);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
      
    public List<string> GetWaitlistEmailsForPackage(int packageId)
    {
            var list = new List<string>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                SELECT UserID
                FROM WaitingList
                WHERE PackageID = @PackageId
                  AND Status = 'Waiting'
                ORDER BY RequestDate ASC;
            ", conn);

            cmd.Parameters.AddWithValue("@PackageId", packageId);

            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(r["UserID"].ToString()!);

            return list;
    }
        
    public int MarkWaitlistNotified(int packageId)
    {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                UPDATE WaitingList
                SET Status = 'Notified'
                WHERE PackageID = @PackageId AND Status = 'Waiting';
            ", conn);

            cmd.Parameters.AddWithValue("@PackageId", packageId);
            return cmd.ExecuteNonQuery();
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
    public List<MyOrderRowVM> GetUserOrders(string userEmail, string? search = null)
    {
        var list = new List<MyOrderRowVM>();

        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        var sql = @"
SELECT
    o.OrderID,
    o.PackageID,
    o.TotalPrice,
    o.Status,
    o.OrderDate,
    o.PaidAt,
    p.Destination,
    p.Country
FROM Orders o
JOIN TravelPackages p ON p.PackageId = o.PackageID
WHERE
    o.UserEmail = @Email
    AND (
        @q IS NULL OR @q = '' OR
        p.Destination LIKE '%' + @q + '%' OR
        p.Country LIKE '%' + @q + '%' OR
        o.Status LIKE '%' + @q + '%' OR
        CAST(o.OrderID AS NVARCHAR(20)) = @q OR
        CAST(o.PackageID AS NVARCHAR(20)) = @q
    )
ORDER BY o.OrderDate DESC;
";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Email", userEmail);
        cmd.Parameters.AddWithValue("@q", (object?)search ?? "");

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new MyOrderRowVM
            {
                OrderID = (int)r["OrderID"],
                PackageID = (int)r["PackageID"],
                Destination = r["Destination"].ToString()!,
                Country = r["Country"].ToString()!,
                TotalPrice = (decimal)r["TotalPrice"],
                Status = r["Status"].ToString()!,
                OrderDate = (DateTime)r["OrderDate"],
                PaidAt = r["PaidAt"] == DBNull.Value ? null : (DateTime?)r["PaidAt"]
            });
        }

        return list;
    }
}