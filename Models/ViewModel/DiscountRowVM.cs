using System.ComponentModel.DataAnnotations;

namespace TripsProject.Models.ViewModel;

public class DiscountRowVM
{
    public int DiscountID { get; set; }

    [Required(ErrorMessage = "Package is required")]
    public int PackageID { get; set; }

    public string Destination { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Original price must be greater than 0")]
    public decimal OriginalPrice { get; set; }

    [Required(ErrorMessage = "Discount percent is required")]
    [Range(1, 100, ErrorMessage = "Discount percent must be between 1 and 100")]
    public int DiscountPercent { get; set; }

    [Required(ErrorMessage = "Start date is required")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "End date is required")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    // Calculated fields
    public decimal FinalPrice => OriginalPrice * (1 - DiscountPercent / 100m);

    public string Status
    {
        get
        {
            var today = DateTime.Today;

            if (today < StartDate.Date)
                return "⏳ Upcoming";

            if (today > EndDate.Date)
                return "❌ Expired";

            return "✅ Active";
        }
    }

}