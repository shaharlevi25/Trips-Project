using Microsoft.AspNetCore.Mvc;
using TripsProject.Data;
using TripsProject.Models;
namespace TripsProject.Controllers.Admin;

public class AdminController : Controller
{
    public IActionResult Dashboard()
    {
        return View();
    }
}


    
            
