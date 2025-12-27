using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TripsProject.Data;
using TripsProject.Models;

namespace TripsProject.Controllers
{
    public class UserController : Controller
    {
        private string connectionString;
        

        public UserController(IConfiguration config)
        {
            connectionString = config.GetConnectionString("TravelDb");
        }
        

        /* הרשמה*/
        [HttpGet]
        public IActionResult Register()
        {
            return View(new User());
        }

        [HttpPost]
        public IActionResult Register(User model)
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
            }

            return Redirect("/");
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string Email, string Password)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string sql = "SELECT * FROM Users WHERE Email = @Email AND Password = @Password";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Email", Email);
                cmd.Parameters.AddWithValue("@Password", Password);

                var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    // התחברות הצליחה
                    return Redirect("/");
                }
                else
                {
                    // שגיאה
                    ViewBag.Error = "Incorrect email or password";
                    return View();
                }
            }
        }
        
        
        
    }
}