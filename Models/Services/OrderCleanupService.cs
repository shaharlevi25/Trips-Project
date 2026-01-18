using Microsoft.Data.SqlClient;

public class OrderCleanupService
{
    private readonly string _connectionString;

    public OrderCleanupService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("TravelDb");
    }

    public int CancelExpiredPendingOrders(int minutes = 10)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            // 1) למצוא הזמנות שפגו
            var cmdGet = new SqlCommand(@"
            SELECT OrderID, PackageID
            FROM Orders
            WHERE Status = 'PendingPayment'
              AND OrderDate < DATEADD(minute, -@Minutes, GETDATE());
        ", conn, tx);

            cmdGet.Parameters.AddWithValue("@Minutes", minutes);

            var expired = new List<(int orderId, int packageId)>();

            using (var r = cmdGet.ExecuteReader())
            {
                while (r.Read())
                    expired.Add(((int)r["OrderID"], (int)r["PackageID"]));
            }

            if (expired.Count == 0)
            {
                tx.Commit();
                return 0;
            }

            foreach (var e in expired)
            {
                var cmdBack = new SqlCommand(@"
                UPDATE TravelPackages
                SET Amount = Amount + 1,
                    IsAvailable = 1
                WHERE PackageId = @PackageId;
            ", conn, tx);

                cmdBack.Parameters.AddWithValue("@PackageId", e.packageId);
                cmdBack.ExecuteNonQuery();

                var cmdCancel = new SqlCommand(@"
                UPDATE Orders
                SET Status = 'Cancelled'
                WHERE OrderID = @OrderID AND Status = 'PendingPayment';
            ", conn, tx);

                cmdCancel.Parameters.AddWithValue("@OrderID", e.orderId);
                cmdCancel.ExecuteNonQuery();
            }

            tx.Commit();
            return expired.Count;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}