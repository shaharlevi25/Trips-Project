using Microsoft.AspNetCore.Mvc;
using TripsProject.Data;

namespace TripsProject.Controllers.Admin;

public class OrdersController : Controller
{
    private readonly OrderRepository _repo;

    public OrdersController(OrderRepository repo)
    {
        _repo = repo;
    }

    public IActionResult Index()
    {   
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