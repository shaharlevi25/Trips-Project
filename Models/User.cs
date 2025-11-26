using System.ComponentModel.DataAnnotations;

namespace TripsProject.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 4)]
        public string Password { get; set; }

        public string Role { get; set; } = "User";   // בעתיד נשתמש לזה ל-Admin
    }
}