namespace TripsProject.Models;

public class CartItem
{
    public int PackageId { get; set; }
    public string Title { get; set; } = "";
    public decimal Price { get; set; }
}