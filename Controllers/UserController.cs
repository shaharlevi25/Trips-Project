using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TripsProject.Data;
using TripsProject.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Threading.Tasks;
using TripsProject.Services;

namespace TripsProject.Controllers
{
    public class UserController : Controller
    {
        private string connectionString;
        private readonly EmailService _emailService;
        

        public UserController(IConfiguration config, EmailService emailService)
        {
            connectionString = config.GetConnectionString("TravelDb");
            _emailService = emailService;
        }
        

        /* הרשמה*/
        [HttpGet]
        public IActionResult Register()
        {
            return View(new User());
        }

        [HttpPost]
        public async Task<IActionResult> Register(User model)
        {
            if (!ModelState.IsValid)
                return View(model);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string sql = @"INSERT INTO Users (FirstName, LastName, Email, Password, PhoneNumber, Role)
                               VALUES (@FirstName, @LastName, @Email, @Password, @PhoneNumber, @Role)";

                SqlCommand cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@FirstName", model.FirstName);
                cmd.Parameters.AddWithValue("@LastName", model.LastName);
                cmd.Parameters.AddWithValue("@PhoneNumber", model.PhoneNumber);
                cmd.Parameters.AddWithValue("@Email", model.Email);
                cmd.Parameters.AddWithValue("@Password", model.Password);
                cmd.Parameters.AddWithValue("@Role", model.Role);

                cmd.ExecuteNonQuery();
                await _emailService.SendAsync(
                    model.Email,
                    "ברוך הבא ל-TripsProject",
                    $"<h2>שלום {model.FirstName}</h2><p>נרשמת בהצלחה למערכת.</p>"
                );
            }

            return Redirect("/");
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string Email, string Password)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string sql = @"SELECT FirstName, Email, Role 
                       FROM Users 
                       WHERE Email = @Email AND Password = @Password";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Email", Email);
                cmd.Parameters.AddWithValue("@Password", Password);

                var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, reader["Email"].ToString()),
                        new Claim(ClaimTypes.Role, reader["Role"].ToString())
                    };

                    var identity = new ClaimsIdentity(
                        claims,
                        CookieAuthenticationDefaults.AuthenticationScheme
                    );

                    var principal = new ClaimsPrincipal(identity);

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        principal
                    );

                    await _emailService.SendAsync(
                        reader["Email"].ToString(),
                        "Login notification",
                        "התחברת בהצלחה למערכת TripsProject"
                    );

                    return Redirect("/");
                }
                else
                {
                    ViewBag.Error = "Incorrect email or password";
                    return View();
                }
            }
        }
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme
            );
            return RedirectToAction("Index", "Trips");
        }
    }
    
}