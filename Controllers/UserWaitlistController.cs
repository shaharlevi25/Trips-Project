using Microsoft.AspNetCore.Mvc;
using TripsProject.Data;

namespace TripsProject.Controllers;

public class UserWaitlistController : Controller
{
    private readonly WaitingListRepository _repo;

    public UserWaitlistController(WaitingListRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public IActionResult MyWaitlist()
    {
        if (!User.Identity!.IsAuthenticated)
            return Unauthorized();

        string email = User.Identity!.Name!;
        var model = _repo.GetMyWaitlist(email);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Leave(int waitingId)
    {
        if (!User.Identity!.IsAuthenticated)
            return Unauthorized();

        string email = User.Identity!.Name!;
        bool ok = _repo.LeaveWaitlist(waitingId, email);

        TempData[ok ? "Success" : "Error"] =
            ok ? "You have left the waitlist." : "Could not leave the waitlist.";

        return RedirectToAction(nameof(MyWaitlist));
    }
    public IActionResult MyWaitlistSearch(string? query)
    {
        if (!User.Identity!.IsAuthenticated)
            return Unauthorized();

        string email = User.Identity!.Name!;
        var model = _repo.GetMyWaitlist(email, query);
        return PartialView("_MyWaitlistTable", model);
    }
}