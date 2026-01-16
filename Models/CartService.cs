using Microsoft.AspNetCore.Http;
using System.Text.Json;
using TripsProject.Models;

namespace TripsProject.Services
{
    public class CartService
    {
        private const string SessionKey = "CART";
        private readonly IHttpContextAccessor _http;

        public CartService(IHttpContextAccessor http)
        {
            _http = http;
        }

        public List<CartItem> GetItems()
        {
            var context = _http.HttpContext;
            if (context == null) return new List<CartItem>();
            var json = context.Session.GetString(SessionKey);
            return json == null
                ? new List<CartItem>()
                : JsonSerializer.Deserialize<List<CartItem>>(json);
        }

        public void Add(int packageId, string title, decimal price, DateTime startDate, DateTime endDate)
        {
            var items = GetItems();

            if (items.Count >= 3)
                return;

            items.Add(new CartItem
            {
                PackageId = packageId,
                Title = title,
                Price = price,
                StartDate = startDate,
                EndDate = endDate
            });

            _http.HttpContext.Session.SetString(
                SessionKey,
                JsonSerializer.Serialize(items)
            );
        }
        public void Remove(int packageId)
        {
            var items = GetItems();
            items = items.Where(i => i.PackageId != packageId).ToList();
            _http.HttpContext.Session.SetString(
                SessionKey,
                JsonSerializer.Serialize(items)
            );
        }
        public int Count()
        {
            return GetItems().Count;
        }

        public decimal TotalPrice()
        {
            return GetItems().Sum(i => i.Price);
        }
    }
    public class CartItem
    {
        public int PackageId { get; set; }
        public string Title { get; set; } = "";
        public decimal Price { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}