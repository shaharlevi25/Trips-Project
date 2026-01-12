namespace TripsProject.ViewModels
{
    public class PackageCardVm
    {
        public TripsProject.Models.TravelPackage Package { get; set; } = null!;
        public int? DiscountPercent { get; set; }
        public decimal? DiscountedPrice { get; set; }
    }
}