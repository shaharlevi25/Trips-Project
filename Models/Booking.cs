namespace TripsProject.Models
{


    public class Booking
    {
        public int BookingId { get; set; }
        public string UserEmail { get; set; }
        public int PackageId { get; set; }
        public int RoomsBooked { get; set; }
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? CancelledAt { get; set; }

        public string Notes { get; set; }
    }
}