using System.ComponentModel.DataAnnotations;

namespace TripsProject.Models.ViewModel;

public class PackageDiscountVM
{
    // פרטי ההנחה
    public int DiscountID { get; set; }

    [Required(ErrorMessage = "בחר חבילה")]
    public int PackageID { get; set; }

    [Required(ErrorMessage = "תאריך התחלה נדרש")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "תאריך סיום נדרש")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    [Required(ErrorMessage = "אחוז הנחה נדרש")]
    [Range(1, 100, ErrorMessage = "אחוז הנחה חייב להיות בין 1 ל-100")]
    public int DiscountPercent { get; set; }

    // פרטי החבילה (לצורך תצוגה ובדיקות)
    public string Destination { get; set; }
    public decimal OriginalPrice { get; set; }
    public DateTime PackageStart { get; set; }  // התחלת החבילה
    public DateTime PackageEnd { get; set; }    // סוף החבילה

    // מחיר אחרי הנחה
    public decimal FinalPrice => OriginalPrice * (1 - DiscountPercent / 100m);

    // בדיקה אם ההנחה בתוקף
    public bool IsActive => DateTime.Today >= StartDate && DateTime.Today <= EndDate;
}