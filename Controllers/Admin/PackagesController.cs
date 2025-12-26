using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripsProject.Data;
using TripsProject.Models;
using System.Linq; 
namespace TripsProject.Controllers.Admin
{
    
    public class PackagesController : Controller
    {
        private readonly PackageRepository _repo;

        public PackagesController(PackageRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index()
        {
            var packages = _repo.GetAllPackages().ToList();
            return View(packages);
        }

        // פעולה שמחזירה רק את טבלת החבילות – למטרות AJAX
        public IActionResult Filter(string destination, DateTime? startDate, DateTime? endDate, bool? isAvailable)
        {
            var packages = _repo.GetAllPackages().AsEnumerable();

            if (!string.IsNullOrEmpty(destination))
                packages = packages.Where(p => p.Destination.Contains(destination, StringComparison.OrdinalIgnoreCase));

            if (startDate.HasValue)
                packages = packages.Where(p => p.StartDate >= startDate.Value);

            if (endDate.HasValue)
                packages = packages.Where(p => p.EndDate <= endDate.Value);

            if (isAvailable.HasValue)
                packages = packages.Where(p => p.IsAvailable == isAvailable.Value);

            return PartialView("_PackagesTable", packages.ToList());
        }

        [HttpPost]
        public IActionResult Create(Package package)
        {
            if (package.StartDate < new DateTime(1753, 1, 1))
            {
                ModelState.AddModelError("StartDate", "Start date is not valid");
            }

            if (package.EndDate < new DateTime(1753, 1, 1))
            {
                ModelState.AddModelError("EndDate", "End date is not valid");
            }

            
            if (!ModelState.IsValid)
            {
                return View(package);
            }
            
            _repo.AddPackage(package);
            return RedirectToAction("Index");
        }
        public IActionResult Create()
        {
            return View(); // מחזיר את ה-View Create.cshtml
        }
        public IActionResult Edit(int id)
        {
            var package = _repo.GetPackageById(id);
            if (package == null)
                return NotFound();

            return View(package);
        }

        [HttpPost]
        public IActionResult Edit(Package package)
        {
            if (package.StartDate < new DateTime(1753, 1, 1))
            {
                ModelState.AddModelError("StartDate", "Start date is not valid");
            }

            if (package.EndDate < new DateTime(1753, 1, 1))
            {
                ModelState.AddModelError("EndDate", "End date is not valid");
            }

            if (!ModelState.IsValid)
                return View(package);

            _repo.UpdatePackage(package);
            return RedirectToAction("Index");
        }
        
    }
}