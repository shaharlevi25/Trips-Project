namespace TripsProject.Models.ViewModel;

public class MyWaitlistRowVM
{
    public int WaitingId { get; set; }
    public int PackageId { get; set; }

    public string Destination { get; set; } = null!;
    public string Country { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public DateTime RequestDate { get; set; }
    public string Status { get; set; } = null!;
}