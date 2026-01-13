using Microsoft.AspNetCore.Mvc;
using TripsProject.Data;
using TripsProject.Models.ViewModel;

namespace TripsProject.Controllers.Admin;

public class DiscountsController : Controller
{
    private readonly DiscountRepository _repo;

    public DiscountsController(DiscountRepository repo)
    {
        _repo = repo;
    }

    // דף ראשי: מציג הכל
    public IActionResult Index()
    {
        var data = _repo.GetAll("");
        return View(data);
    }

    // AJAX: מחזיר רק את הטבלה
    [HttpGet]
    public IActionResult Filter(string search)
    {
        var data = _repo.GetAll(search ?? "");
        return PartialView("_DiscountsTable", data);
    }
    
    
    public IActionResult Add()
        {
            return View();
        }

        // AJAX: load packages table
        [HttpGet]
        public IActionResult PackagesPicker(string search)
        {
            var packages = _repo.GetPackagesForPicker(search ?? "");
            return PartialView("_PackagesPickerTable", packages);
        }

        // GET: /Discounts/Create?packageId=5  (discount form)
        public IActionResult Create(int packageId)
        {
            var info = _repo.GetPackageInfo(packageId);
            if (info == null) return NotFound();

            // default discount dates inside package dates
            info.StartDate = info.PackageStart;
            info.EndDate = info.PackageEnd;
            info.DiscountPercent = 10;

            return View(info); // DiscountVM
        }

        // POST: Create discount with validations
        [HttpPost]
        public IActionResult Create(DiscountVM vm)
        {
            // load package boundaries for validation & display
            var info = _repo.GetPackageInfo(vm.PackageID);
            if (info == null)
            {
                ModelState.AddModelError("PackageID", "Package not found");
                return View(vm);
            }

            // keep package info on screen if errors
            vm.Destination = info.Destination;
            vm.Country = info.Country;
            vm.OriginalPrice = info.OriginalPrice;
            vm.PackageStart = info.PackageStart;
            vm.PackageEnd = info.PackageEnd;

            // 1) DataAnnotations
            if (!ModelState.IsValid)
                return View(vm);

            // 2) StartDate <= EndDate
            if (vm.StartDate.Date > vm.EndDate.Date)
                ModelState.AddModelError("", "Start date cannot be after end date");

            // 3) Discount dates inside package dates
            if (vm.EndDate.Date > vm.PackageEnd.Date)
                ModelState.AddModelError("", "Discount dates must be within the package dates");
            if (_repo.HasOverlap(vm.PackageID, vm.StartDate, vm.EndDate))
                ModelState.AddModelError("", "This package already has a discount overlapping these dates.");

            // 4) Only one active discount now (today) for the package
            if (_repo.HasActiveDiscountNow(vm.PackageID))
                ModelState.AddModelError("", "This package already has an active discount right now");
            int diffDays = (vm.EndDate.Date - vm.StartDate.Date).Days;
            if (diffDays > 7)
            {
                ModelState.AddModelError(
                    "",
                    "Discount dates must be within a week"
                );
            }
            

            if (!ModelState.IsValid)
                return View(vm);

            _repo.AddDiscount(vm);
            return RedirectToAction("Index");
        }
        // GET: /Discounts/Edit/5
        public IActionResult Edit(int id)
        {
            var vm = _repo.GetDiscountForEdit(id);
            if (vm == null) return NotFound();

            return View(vm);
        }

// POST: /Discounts/Edit
        [HttpPost]
        public IActionResult Edit(DiscountVM vm)
        {
            // טוענים שוב מידע חבילה+הנחה מה DB כדי לא לסמוך על ערכים שהגיעו מהמשתמש
            var current = _repo.GetDiscountForEdit(vm.DiscountID);
            if (current == null) return NotFound();

            // נשמור את פרטי החבילה לתצוגה ולבדיקות
            vm.PackageID = current.PackageID;     // לא מאפשרים לשנות חבילה בעריכה
            vm.Destination = current.Destination;
            vm.Country = current.Country;
            vm.OriginalPrice = current.OriginalPrice;
            vm.PackageStart = current.PackageStart;
            vm.PackageEnd = current.PackageEnd;

            // DataAnnotations
            if (!ModelState.IsValid)
                return View(vm);

            // 1) StartDate <= EndDate
            if (vm.StartDate.Date > vm.EndDate.Date)
                ModelState.AddModelError("", "Start date cannot be after end date");

            // 2) within package dates
            if ( vm.EndDate.Date > vm.PackageEnd.Date)
                ModelState.AddModelError("", "Discount dates must be within the package dates");
            if (_repo.HasOverlap(vm.PackageID, vm.StartDate, vm.EndDate))
                ModelState.AddModelError("", "This package already has a discount overlapping these dates.");
           
            int diffDays = (vm.EndDate.Date - vm.StartDate.Date).Days;
            if (diffDays > 7)
            {
                ModelState.AddModelError(
                    "",
                    "Discount dates must be within a week"
                );
            }

            // 3) only one active discount now (exclude this discount)
            if (_repo.HasActiveDiscountNow(vm.PackageID, excludeDiscountId: vm.DiscountID))
                ModelState.AddModelError("", "This package already has another active discount right now");

            if (!ModelState.IsValid)
                return View(vm);

            _repo.UpdateDiscount(vm);
            return RedirectToAction("Index");
        }
        // GET: /Discounts/Delete/5  -> confirmation page
        public IActionResult Delete(int id)
        {
            var vm = _repo.GetDiscountForEdit(id); // כבר מחזיר הכל (יעד, מחיר, תאריכים...)
            if (vm == null) return NotFound();

            return View(vm);
        }

// POST: /Discounts/DeleteConfirmed
        [HttpPost]
        public IActionResult DeleteConfirmed(int discountId)
        {
            _repo.DeleteDiscount(discountId);
            return RedirectToAction("Index");
        }
        

}