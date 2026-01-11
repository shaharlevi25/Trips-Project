namespace TripsProject.Models
{
    public class TravelPackage
    {
        public int PackageId { get; set; }
        public string Destination { get; set; } = "";
        public string Country { get; set; } = "";

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public decimal Price { get; set; }
        public int NumOfRooms { get; set; }
        
        public int NumOfPeople { get; set; }


        public string PackageType { get; set; } = "";
        public int? AgeLimit { get; set; }

        public string Description { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        public bool IsAvailable { get; set; }

        // אופציונלי אם יש לך תמונות:
        public string? ImageUrl { get; set; }
        
        public int Amount  { get; set; } 
    }
}