using System.ComponentModel.DataAnnotations;
using TripsProject.Validation;

namespace TripsProject.Models;

public class Package
{
    public int PackageId { get; set; }
    
    [Required]
    public string Destination { get; set; }
    [Required]
    public string Country { get; set; }
    [Required]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }
    [Required]
    [DataType(DataType.Date)]
    [DateGreaterThan("StartDate", ErrorMessage = "End date must be after start date")]
    public DateTime EndDate { get; set; }
    public decimal Price { get; set; }  
    public int NumOfRooms { get; set; }
    public string PackageType { get; set; }
    public int AgeLimit { get; set; }
    public string Description { get; set; }
    public bool IsAvailable { get; set; }
}