namespace TripsProject.Models.ViewModel;

public class WaitingListRowVM
{
    public int WaitingID { get; set; }
    public string UserID { get; set; }
    public int PackageID { get; set; }
    public string Destination { get; set; }
    public DateTime? RequestDate { get; set; }
    public string Status { get; set; }
}