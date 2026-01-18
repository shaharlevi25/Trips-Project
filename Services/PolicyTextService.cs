using System.Text;
using TripsProject.Models.ViewModel;

namespace TripsProject.Services;

public class PolicyTextService
{
    public string BuildPolicyText(BookingRulesVM rules)
    {
       {
            if (rules == null)
                return "Booking and cancellation policy is currently unavailable. Please try again later.";

            var sb = new StringBuilder();

            if (rules.LatestBookingDaysBeforeStart == 0)
            {
                sb.Append("Booking is available until the trip start date. ");
            }
            else if (rules.LatestBookingDaysBeforeStart == 1)
            {
                sb.Append("Bookings must be completed no later than 1 day before the trip start date. ");
            }
            else
            {
                sb.Append($"Bookings must be completed no later than {rules.LatestBookingDaysBeforeStart} days before the trip start date. ");
            }

            if (rules.CancellationDaysBeforeStart == 0)
            {
                sb.Append("Cancellations are permitted until the trip start date. ");
            }
            else if (rules.CancellationDaysBeforeStart == 1)
            {
                sb.Append("Cancellations are permitted up to 1 day before the trip start date. ");
            }
            else
            {
                sb.Append($"Cancellations are permitted up to {rules.CancellationDaysBeforeStart} days before the trip start date. ");
            }

            if (rules.ReminderDaysBeforeStart == 0)
            {
                sb.Append("No automated reminder notifications are scheduled. ");
            }
            else if (rules.ReminderDaysBeforeStart == 1)
            {
                sb.Append("A reminder notification may be sent 1 day prior to departure. ");
            }
            else
            {
                sb.Append($"A reminder notification may be sent {rules.ReminderDaysBeforeStart} days prior to departure. ");
            }

            sb.Append("All time limits are calculated based on the trip's scheduled start date (local time). ");
            sb.Append("Policies may be updated by the administrator and will apply to future bookings according to the latest published rules.");

            return sb.ToString();
        }
    }
}