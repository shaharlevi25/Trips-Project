using Microsoft.Data.SqlClient;
using TripsProject.Models;
using TripsProject.Models.ViewModel;

namespace TripsProject.Data;

public class BookingRulesRepository
{
    private readonly string _cs;

    public BookingRulesRepository(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("TravelDb");
    }

    public BookingRulesVM Get()
    {
        using var conn = new SqlConnection(_cs);
        conn.Open();

        var cmd = new SqlCommand("SELECT TOP 1 * FROM BookingRules WHERE RulesId = 1", conn);
        using var r = cmd.ExecuteReader();

        if (!r.Read()) return null;

        return new BookingRulesVM
        {
            RulesId = (int)r["RulesId"],
            LatestBookingDaysBeforeStart = (int)r["LatestBookingDaysBeforeStart"],
            CancellationDaysBeforeStart = (int)r["CancellationDaysBeforeStart"],
            ReminderDaysBeforeStart = (int)r["ReminderDaysBeforeStart"]
        };
    }

    public void Upsert(BookingRulesVM vm)
    {
        using var conn = new SqlConnection(_cs);
        conn.Open();

        var cmd = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM BookingRules WHERE RulesId = 1)
BEGIN
    UPDATE BookingRules
    SET LatestBookingDaysBeforeStart = @latest,
        CancellationDaysBeforeStart = @cancel,
        ReminderDaysBeforeStart = @reminder
    WHERE RulesId = 1
END
ELSE
BEGIN
    INSERT INTO BookingRules (RulesId, LatestBookingDaysBeforeStart, CancellationDaysBeforeStart, ReminderDaysBeforeStart)
    VALUES (1, @latest, @cancel, @reminder)
END
", conn);

        cmd.Parameters.AddWithValue("@latest", vm.LatestBookingDaysBeforeStart);
        cmd.Parameters.AddWithValue("@cancel", vm.CancellationDaysBeforeStart);
        cmd.Parameters.AddWithValue("@reminder", vm.ReminderDaysBeforeStart);

        cmd.ExecuteNonQuery();
    }
}
