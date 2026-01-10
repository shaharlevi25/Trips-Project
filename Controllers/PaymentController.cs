using Microsoft.AspNetCore.Mvc;

namespace TripsProject.Controllers
{
    public class PaymentController : Controller
    
    {
        public IActionResult Success()
        {
            ViewBag.Msg = "התשלום בוצע בהצלחה (Sandbox)";
            return View();
        }

        public IActionResult Cancel()
        {
            ViewBag.Msg = "התשלום בוטל";
            return View();
        }
        public IActionResult Checkout()
        {
            return View();
        }
    }
}