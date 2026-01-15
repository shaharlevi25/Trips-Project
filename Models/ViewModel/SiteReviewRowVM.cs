namespace TripsProject.Models.ViewModel;

public class SiteReviewRowVM
{
    public int ReviewId { get; set; }
    public string FullName { get; set; } = "";   // FirstName + LastName
    public int Rating { get; set; }              // 1..5
    public string Comment { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}