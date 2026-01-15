using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripsProject.Data;

namespace TripsProject.Controllers.Admin;

[Authorize(Roles = "Admin")]
public class BookingsController : Controller
{
    private readonly BookingRepository _repo;

    public BookingsController(BookingRepository repo)
    {
        _repo = repo;
    }

    public IActionResult Index()
    {
        var bookings = _repo.GetAll();
        return View(bookings);
    }

    [HttpGet]
    public IActionResult Search(string? query)
    {
        var bookings = _repo.GetAll(query);
        return PartialView("_BookingsTable", bookings);
    }
}