using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TripsProject.Models;

namespace TripsProject.Controllers;

public class HomePageController : Controller
{
    private readonly ILogger<HomePageController> _logger;

    public HomePageController(ILogger<HomePageController> logger)
    {
        _logger = logger;
    }

  
    public IActionResult HomePage()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}