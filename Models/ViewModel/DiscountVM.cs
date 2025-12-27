using System.ComponentModel.DataAnnotations;

namespace TripsProject.Models.ViewModel;

public class DiscountVM
{
    public int DiscountID { get; set; }

    [Required(ErrorMessage = "Package is required")]
    public int PackageID { get; set; }

    [Required(ErrorMessage = "Discount percent is required")]
    [Range(1, 100, ErrorMessage = "Discount percent must be between 1 and 100")]
    public int DiscountPercent { get; set; }

    [Required(ErrorMessage = "Start date is required")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "End date is required")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    // Package info (for list + picker + validation)
    public string? Destination { get; set; }
    public string? Country { get; set; }
    public decimal OriginalPrice { get; set; }
    public DateTime PackageStart { get; set; }
    public DateTime PackageEnd { get; set; }

    // Calculated
    public decimal FinalPrice => OriginalPrice * (1 - DiscountPercent / 100m);

    public bool IsActive =>
        DateTime.Today >= StartDate.Date &&
        DateTime.Today <= EndDate.Date;

    // For searching (optional convenience)
    public string? Search { get; set; }
}