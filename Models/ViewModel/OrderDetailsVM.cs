namespace TripsProject.Models.ViewModel;

public class OrderDetailsVM
{
    public int OrderID { get; set; }
    public string UserEmail { get; set; } = "";
    public int PackageID { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = "";
    public DateTime? PaidAt { get; set; }
    public string Destination { get; set; } = "";
    public string Country { get; set; } = "";
}