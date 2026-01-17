using Microsoft.AspNetCore.Mvc;
using TripsProject.Data;

namespace TripsProject.Controllers;

[Route("Waitlist")]
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
    [HttpPost("Join")]
    public IActionResult Join([FromBody] JoinWaitlistRequest? request)
    {
        if (!User.Identity!.IsAuthenticated)
            return Unauthorized();

        int packageId = request?.PackageId ?? 0;
        if (packageId <= 0)
            return BadRequest(new { status = "bad_request" });

        string email = User.Identity!.Name!;

        var result = _repo.TryJoinWaitlist(packageId, email);

        return result switch
        {
            WaitingListRepository.JoinResult.Inserted => Json(new { status = "ok" }),
            WaitingListRepository.JoinResult.AlreadyExists => Json(new { status = "already_exists" }),
            _ => StatusCode(500, new { status = "error" })
        };
    }

    public sealed record JoinWaitlistRequest(int PackageId);
    public IActionResult MyWaitlistSearch(string? query)
    {
        if (!User.Identity!.IsAuthenticated)
            return Unauthorized();

        string email = User.Identity!.Name!;
        var model = _repo.GetMyWaitlist(email, query);
        return PartialView("_MyWaitlistTable", model);
    }
}