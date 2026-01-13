namespace TripsProject.Models;

public class BookingRules
{
    public int RulesId { get; set; }
    public int LatestBookingDaysBeforeStart { get; set; }
    public int CancellationDaysBeforeStart { get; set; }
    public int ReminderDaysBeforeStart { get; set; }
   
}