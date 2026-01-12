namespace TripsProject.Models.ViewModel;

public class OrderRowVM
{
    public int OrderId { get; set; }
    public string UserEmail { get; set; }

    public int PackageId { get; set; }
    public string Destination { get; set; }

    public decimal TotalPrice { get; set; }
    public string Status { get; set; }

    public DateTime OrderDate { get; set; }
    public DateTime? PaidAt { get; set; }

    public string? PayPalOrderId { get; set; }
}