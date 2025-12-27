using System.ComponentModel.DataAnnotations;

namespace TripsProject.Models;

public class PackageDiscount
{
    public int DiscountID { get; set; }
    public int PackageID { get; set; }

    [Range(1, 100, ErrorMessage = "Discount must be between 1 and 100")]
    public int DiscountPercent { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}