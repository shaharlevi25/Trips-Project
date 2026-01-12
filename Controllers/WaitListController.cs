using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace TripsProject.Controllers
{
    [Route("Waitlist")]
    public class WaitlistController : Controller
    {
        private readonly string _connectionString;

        public WaitlistController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("TravelDb")!;
        }

        public class JoinWaitlistRequest
        {
            public int PackageId { get; set; }
        }

        // POST: /Waitlist/Join
        [HttpPost("Join")]
        public IActionResult Join([FromBody] JoinWaitlistRequest req)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized();
            }

            var email = User.Identity.Name;


            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            // 2) בדיקת כפילות
            using (var checkCmd = new SqlCommand(@"
                SELECT COUNT(*)
                FROM dbo.WaitingList
                WHERE UserID = @email AND PackageID = @pid
            ", conn))
            {
                checkCmd.Parameters.AddWithValue("@email", email);
                checkCmd.Parameters.AddWithValue("@pid", req.PackageId);

                int exists = (int)checkCmd.ExecuteScalar();
                if (exists > 0)
                {
                    return Json(new { status = "already_exists" });
                }
            }

            // 3) הכנסה לטבלה
            using (var insertCmd = new SqlCommand(@"
                INSERT INTO dbo.WaitingList (UserID, PackageID, RequestDate, Status)
                VALUES (@email, @pid, GETDATE(), @status)
            ", conn))
            {
                insertCmd.Parameters.AddWithValue("@email", email);
                insertCmd.Parameters.AddWithValue("@pid", req.PackageId);
                insertCmd.Parameters.AddWithValue("@status", "Pending"); // מתאים כי אצלך Status nvarchar(20)

                insertCmd.ExecuteNonQuery();
            }

            return Json(new { status = "ok" });
        }
    }
}
