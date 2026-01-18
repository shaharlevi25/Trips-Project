using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TripsProject.Controllers
{
    public class DashboardController : Controller
    {
        [Authorize] 
        [HttpGet]
        public IActionResult User()
        {
            return View();
        }
        
        public IActionResult Details()
        {
            return View();
        }
    }
}