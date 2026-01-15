using System.ComponentModel.DataAnnotations;

namespace TripsProject.Models.ViewModel;

public class SiteReviewCreateVM
{
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
    public int Rating { get; set; } = 5;

    [Required]
    [StringLength(1000, MinimumLength = 3, ErrorMessage = "Comment must be 3-1000 characters")]
    public string Comment { get; set; } = "";
}