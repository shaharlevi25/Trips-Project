using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TripsProject.Models;

namespace TripsProject.Controllers
{
    [Authorize]
    public class BookingsController : Controller
    {
        private readonly string _connectionString;

        public BookingsController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("TravelDb");
        }

        [HttpGet]
        public IActionResult MyBookings()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login", "User");

            var bookings = new List<Booking>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string sql = @"SELECT * FROM Bookings WHERE UserEmail = @Email and Status <> 'Cancelled'" ;

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Email", email);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    bookings.Add(new Booking
                    {
                        BookingId = (int)reader["BookingId"],
                        UserEmail = reader["UserEmail"].ToString(),
                        PackageId = (int)reader["PackageId"],
                        RoomsBooked = (int)reader["RoomsBooked"],
                        TotalAmount = (decimal)reader["TotalAmount"],
                        Currency = reader["Currency"].ToString(),
                        Status = reader["Status"].ToString(),

                        CreatedAt = (DateTime)reader["CreatedAt"],
                        PaidAt = reader["PaidAt"] == DBNull.Value ? null : (DateTime)reader["PaidAt"],
                        CancelledAt = reader["CancelledAt"] == DBNull.Value ? null : (DateTime)reader["CancelledAt"],

                        Notes = reader["Notes"] == DBNull.Value ? "" : reader["Notes"].ToString()
                    });

                }
            }

            return View("~/Views/Dashboard/Bookings.cshtml", bookings);
            
            
        }
        
        public IActionResult Cancel(int bookingId)
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login", "User");

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var tx = conn.BeginTransaction();

            try
            {
                // 1) להביא פרטי הזמנה של המשתמש (PackageId + RoomsBooked)
                int packageId;
                int roomsBooked;

                var getBookingSql = @"
                    SELECT PackageId, RoomsBooked
                    FROM dbo.Bookings
                    WHERE BookingId = @BookingId
                      AND UserEmail = @Email
                      AND Status <> 'Cancelled';
                ";

                using (var cmd = new SqlCommand(getBookingSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@BookingId", bookingId);
                    cmd.Parameters.AddWithValue("@Email", email);

                    using var r = cmd.ExecuteReader();
                    if (!r.Read())
                    {
                        tx.Rollback();
                        TempData["Msg"] = "לא נמצאה הזמנה לביטול (או שכבר בוטלה).";
                        return RedirectToAction("MyBookings");
                    }

                    packageId = (int)r["PackageId"];
                    roomsBooked = (int)r["RoomsBooked"];
                }

                // 2) להביא את StartDate של החבילה
                DateTime startDate;
                var getStartSql = @"SELECT StartDate FROM dbo.TravelPackages WHERE PackageId = @PackageId;";
                using (var cmd = new SqlCommand(getStartSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@PackageId", packageId);
                    startDate = (DateTime)cmd.ExecuteScalar();
                }

                // 3) להביא CancellationDaysBeforeStart מתוך BookingRules
                // אם הטבלה שלך היא "כלל אחד גלובלי" -> TOP 1
                int cancelDays;
                var getRuleSql = @"SELECT TOP 1 CancellationDaysBeforeStart FROM dbo.BookingRules;";
                using (var cmd = new SqlCommand(getRuleSql, conn, tx))
                {
                    cancelDays = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // 4) בדיקה: מותר לבטל אם היום <= startDate - cancelDays
                var latestCancelDate = startDate.Date.AddDays(-cancelDays);
                if (DateTime.Now.Date > latestCancelDate)
                {
                    tx.Rollback();
                    TempData["Msg"] = $"לא ניתן לבטל: מותר עד {latestCancelDate:dd/MM/yyyy}.";
                    return RedirectToAction("MyBookings");
                }

                // 5) לבטל את ההזמנה
                var cancelSql = @"
                    UPDATE dbo.Bookings
                    SET Status = 'Cancelled',
                        CancelledAt = GETDATE()
                    WHERE BookingId = @BookingId
                      AND UserEmail = @Email
                      AND Status <> 'Cancelled';
                ";

                using (var cmd = new SqlCommand(cancelSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@BookingId", bookingId);
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.ExecuteNonQuery();
                }

                // 6) להחזיר מלאי: Amount += RoomsBooked (או += 1 אם אצלך זה יחידות)
                var restoreSql = @"
                    UPDATE dbo.TravelPackages
                    SET Amount = Amount + @Rooms
                    WHERE PackageId = @PackageId;
                ";

                using (var cmd = new SqlCommand(restoreSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@Rooms", roomsBooked);
                    cmd.Parameters.AddWithValue("@PackageId", packageId);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                TempData["Msg"] = "ההזמנה בוטלה והמלאי עודכן בהצלחה.";
                return RedirectToAction("MyBookings");
            }
            catch
            {
                tx.Rollback();
                TempData["Msg"] = "שגיאה בביטול ההזמנה. נסה שוב.";
                return RedirectToAction("MyBookings");
            }
        }

        
        
    }
}