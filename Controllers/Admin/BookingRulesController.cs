using Microsoft.AspNetCore.Mvc;
using TripsProject.Data;
using TripsProject.Models;
using TripsProject.Models.ViewModel;
using TripsProject.Services;

namespace TripsProject.Controllers.Admin;

public class BookingRulesController : Controller
{
    private readonly BookingRulesRepository _repo;
    private readonly PolicyTextService _policy;

    public BookingRulesController(BookingRulesRepository repo, PolicyTextService policy)
    {
        _repo = repo;
        _policy = policy;
    }

    // GET: /BookingRules
    public IActionResult Index()
    {
        var vm = _repo.Get() ?? new BookingRulesVM(); // אם אין שורה עדיין
        ViewBag.PolicyText = _policy.BuildPolicyText(vm);
        return View(vm);
    }

    // POST: /BookingRules
    [HttpPost]
    public IActionResult Index(BookingRulesVM vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.PolicyText = _policy.BuildPolicyText(vm);
            return View(vm);
        }

        _repo.Upsert(vm);

        // להציג שוב עם טקסט מעודכן
        ViewBag.PolicyText = _policy.BuildPolicyText(vm);
        ViewBag.Success = "Rules updated successfully.";
        return View(vm);
    }
    
}