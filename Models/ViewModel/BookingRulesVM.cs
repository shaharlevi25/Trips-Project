using System.ComponentModel.DataAnnotations;

namespace TripsProject.Models.ViewModel;

public class BookingRulesVM
{
    public int RulesId { get; set; }

    [Required(ErrorMessage = "Latest booking days is required")]
    [Range(0, 365, ErrorMessage = "Latest booking days must be between 0 and 365")]
    public int LatestBookingDaysBeforeStart { get; set; }

    [Required(ErrorMessage = "Cancellation days is required")]
    [Range(0, 365, ErrorMessage = "Cancellation days must be between 0 and 365")]
    public int CancellationDaysBeforeStart { get; set; }

    [Required(ErrorMessage = "Reminder days is required")]
    [Range(0, 365, ErrorMessage = "Reminder days must be between 0 and 365")]
    public int ReminderDaysBeforeStart { get; set; }
    
}