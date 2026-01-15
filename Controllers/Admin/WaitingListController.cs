using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripsProject.Data;

namespace TripsProject.Controllers.Admin;
[Authorize(Roles = "Admin")]
public class WaitingListController : Controller
{
    // GET
    private readonly WaitingListRepository _repo;

    public WaitingListController(WaitingListRepository repo)
    {
        _repo = repo;
    }

    public IActionResult Index(string? query)
    {
        var items = _repo.GetAll(query);
        return View(items);
    }
    public IActionResult Search(string? query)
    {
        var items = _repo.GetAll(query);
        return PartialView("_WaitingTable", items);
    }
}