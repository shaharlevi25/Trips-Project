using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TripsProject.Data;
using TripsProject.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
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

            return RedirectToAction("Login", "User");
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
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return View();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string sql = @"SELECT FirstName, Email FROM Users WHERE Email = @Email";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Email", email);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                {
                    ViewBag.Sent = true;
                    ViewBag.Mode = "sent";
                    return View();
                }

                var userEmail = reader["Email"].ToString();
                var firstName = reader["FirstName"].ToString();

                var link = Url.Action(
                    "ResetPassword",
                    "User",
                    new { email = userEmail },
                    Request.Scheme
                );

                await _emailService.SendAsync(
                    userEmail,
                    "איפוס סיסמה",
                    $"<h2>שלום {firstName}</h2><p>לחץ כאן לאיפוס הסיסמה:</p><p><a href='{link}'>איפוס סיסמה</a></p>"
                );

                ViewBag.Sent = true;
                ViewBag.Mode = "sent";
                return View();
            }
        }

        [HttpGet]
        public IActionResult ResetPassword(string email)
        {
            ViewBag.Mode = "reset";
            ViewBag.Email = email;
            return View("ForgotPassword");
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string email, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword))
                return View(model: email);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string sql = @"UPDATE Users SET Password = @Password WHERE Email = @Email";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Email", email);
                cmd.Parameters.AddWithValue("@Password", newPassword);

                cmd.ExecuteNonQuery();
            }

            await _emailService.SendAsync(
                email,
                "הסיסמה עודכנה",
                "<p>הסיסמה שלך עודכנה בהצלחה. אם לא אתה ביצעת את הפעולה, פנה לתמיכה.</p>"
            );

            ViewBag.Mode = "request";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme
            );
            return RedirectToAction("Index", "Trips");
        }
        // Details Getter
        [HttpGet]
        public IActionResult Details()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login");

            string email = User.Identity.Name;

            User user = null;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string sql = "SELECT * FROM Users WHERE Email = @Email";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Email", email);

                var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    user = new User
                    {
                        FirstName = reader["FirstName"].ToString(),
                        LastName = reader["LastName"].ToString(),
                        Email = reader["Email"].ToString(),
                        PhoneNumber = reader["PhoneNumber"].ToString(),
                        Password = reader["Password"].ToString()
                    };
                }
            }

            return View(Details);
        }
        
        // Save Details
        
        [HttpPost]
        public IActionResult SaveDetails(User model)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string sql = @"
            UPDATE Users
            SET FirstName = @FirstName,
                LastName = @LastName,
                PhoneNumber = @Phone,
                Password = @Password
            WHERE Email = @Email
        ";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@FirstName", model.FirstName);
                cmd.Parameters.AddWithValue("@LastName", model.LastName);
                cmd.Parameters.AddWithValue("@Phone", model.PhoneNumber);
                cmd.Parameters.AddWithValue("@Password", model.Password);
                cmd.Parameters.AddWithValue("@Email", model.Email);

                cmd.ExecuteNonQuery();
            }

            TempData["Msg"] = "Profile updated successfully!";
            return RedirectToAction("Details");
        }


    }
    
    
}