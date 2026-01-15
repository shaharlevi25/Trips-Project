namespace TripsProject.Models.ViewModel;

public class SiteReviewsPageVM
{
    
    public double AvgRating { get; set; }
    public int ReviewsCount { get; set; }

    public SiteReviewCreateVM NewReview { get; set; } = new SiteReviewCreateVM();
    public List<SiteReviewRowVM> Reviews { get; set; } = new();
}
