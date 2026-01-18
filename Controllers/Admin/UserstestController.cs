using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripsProject.Data;
using TripsProject.Models;

namespace TripsProject.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserstestController : Controller
    {
        private readonly UserRepository _repo;

        public UserstestController(UserRepository repo)
        {
            _repo = repo;
        }

        
        public IActionResult Index()
        {
            var users = _repo.GetAllUsers();
            return View(users);
        }

        // הצגת טופס עריכה
        public IActionResult Edit(string email)
        {
            var user = _repo.GetUserByEmail(email);
            if (user == null)
                return NotFound();

            return View(user);
        }

        [HttpPost]
        public IActionResult Edit(User user)
        {
            if (!ModelState.IsValid)
                return View(user);

            _repo.UpdateUser(user);
            return RedirectToAction("Index");
        }

        // מחיקת משתמש
        public IActionResult Delete(string email)
        {
            var user = _repo.GetUserByEmail(email);
            if (user == null)
                return NotFound();

            _repo.DeleteUser(email);
            return RedirectToAction("Index");
        }
        
        [HttpGet]
        public IActionResult Search(string query)
        {
            if (string.IsNullOrEmpty(query))
                return Json(_repo.GetAllUsers()); // מחזיר את כל המשתמשים אם אין חיפוש

            var users = _repo.GetAllUsers()
                .Where(u => u.FirstName.Contains(query, StringComparison.OrdinalIgnoreCase) 
                            || u.LastName.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || u.Email.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Json(users);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

// קבלת הנתונים ושמירה
        [HttpPost]
        public IActionResult Create(User user)
        {
            if (!ModelState.IsValid)
                return View(user);

            _repo.AddUser(user);
            return RedirectToAction("Index");
        }
        
    }
}