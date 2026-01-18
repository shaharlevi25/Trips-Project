using Microsoft.AspNetCore.Mvc;
using TripsProject.Data;
using TripsProject.Models.ViewModel;

namespace TripsProject.Controllers
{
    public class SiteReviewsController : Controller
    {
        private readonly SiteReviewsRepository _repo;

        public SiteReviewsController(SiteReviewsRepository repo)
        {
            _repo = repo;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var (avg, cnt) = _repo.GetStats();
            var reviews = _repo.GetAll();

            var vm = new SiteReviewsPageVM
            {
                AvgRating = avg,
                ReviewsCount = cnt,
                Reviews = reviews,
                NewReview = new SiteReviewCreateVM()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(SiteReviewsPageVM vm)
        {
            if (!User.Identity.IsAuthenticated)
            {
                TempData["AuthMessage"] = "To leave a review you must register or login.";
                return RedirectToAction("Login", "User");
            }

            string email = User.Identity!.Name!;

            if (_repo.HasUserReviewed(email))
            {
                TempData["error"] = "You already submitted a review. Thank you!";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                var (avg, cnt) = _repo.GetStats();
                vm.AvgRating = avg;
                vm.ReviewsCount = cnt;
                vm.Reviews = _repo.GetAll();
                return View("Index", vm);
            }

            _repo.AddReview(email, vm.NewReview.Rating, vm.NewReview.Comment.Trim());

            return RedirectToAction("Thanks");
        }

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Thanks()
        {
            return View();
        }

        [HttpGet]
        public IActionResult All()
        {
            var (avg, cnt) = _repo.GetStats();
            var vm = new SiteReviewsPageVM
            {
                AvgRating = avg,
                ReviewsCount = cnt,
                Reviews = _repo.GetAll()
            };
            return View(vm);
        }
    }
}
