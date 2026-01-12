using Microsoft.AspNetCore.Mvc;
using TripsProject.Data;

namespace TripsProject.Controllers.Admin;

public class OrdersController : Controller
{
    private readonly OrderCleanupService _cleanup;
    private readonly OrderRepository _repo;

    public OrdersController(OrderRepository repo,OrderCleanupService cleanup)
    {
        _repo = repo;
        _cleanup = cleanup;
    }

    public IActionResult Index()
    {   
        _cleanup.CancelExpiredPendingOrders(10);
        var orders = _repo.GetAll();
        return View(orders);
    }

    [HttpGet]
    public IActionResult Search(string? query)
    {
        var orders = _repo.GetAll(query);
        return PartialView("_OrdersTable", orders);
    }
}