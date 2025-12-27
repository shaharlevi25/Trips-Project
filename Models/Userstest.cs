using System.ComponentModel.DataAnnotations;

namespace TripsProject.Models;

public class Userstest
{
    [Required(ErrorMessage = "First name is required")]
    [StringLength(50)]
    public string FirstName { get; set; }

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50)]
    public string LastName { get; set; }

    [Required(ErrorMessage = "Phone number is required")]
    [Phone(ErrorMessage = "Invalid phone number")]
    public string PhoneNumber { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 4)]
    public string Password { get; set; }

    public string Role { get; set; } = "User";   // בעתיד נשתמש לזה ל-Admin
    public bool IsActive { get; set; } = true;
}