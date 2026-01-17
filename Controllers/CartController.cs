using Microsoft.AspNetCore.Mvc;
using TripsProject.Services;

namespace TripsProject.Controllers
{
    [Route("Cart")]
    public class CartController : Controller
    {
        private readonly CartService _cart;

        public CartController(CartService cart)
        {
            _cart = cart;
        }

        [HttpPost("Add")]
        public IActionResult Add(
            int packageId,
            string title,
            decimal price,
            DateTime startDate,
            DateTime endDate,
            int numOfPeople,
            string? packageType)
        {
            _cart.Add(packageId, title, price, startDate, endDate, numOfPeople, packageType);
            return Json(new { ok = true });
        }

        [HttpGet("Items")]
        public IActionResult Items()
        {
            return Json(_cart.GetItems());
        }
        [HttpGet("Count")]
        public IActionResult Count()
        {
            return Json(new { count = _cart.Count() });
        }
        [HttpPost("Remove")]
        public IActionResult Remove(int packageId)
        {
            _cart.Remove(packageId);
            return Json(new { ok = true });
        }
        [HttpGet("Total")]
        public IActionResult Total()
        {
            return Json(new { total = _cart.TotalPrice() });
        }
    }
}