namespace TripsProject.Models;

public class WaitingListItem
{
    public int WaitingID { get; set; }
    public string UserID { get; set; }
    public int PackageID { get; set; }
    public DateTime? RequestDate { get; set; }
    public string Status { get; set; }
}