namespace TripsProject.Models.ViewModel;

public class MyOrderRowVM
{
    public int OrderID { get; set; }
    public int PackageID { get; set; }
    public string Destination { get; set; } = "";
    public string Country { get; set; } = "";

    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public DateTime? PaidAt { get; set; }
}