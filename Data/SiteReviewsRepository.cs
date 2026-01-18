using Microsoft.Data.SqlClient;
using TripsProject.Models.ViewModel;

namespace TripsProject.Data
{
    public class SiteReviewsRepository
    {
        private readonly string _cs;

        public SiteReviewsRepository(IConfiguration config)
        {
            _cs = config.GetConnectionString("TravelDb");
        }

        public (double avg, int count) GetStats()
        {
            using var conn = new SqlConnection(_cs);
            conn.Open();

            using var cmd = new SqlCommand(@"
SELECT 
    AVG(CAST(Rating AS FLOAT)) AS AvgRating,
    COUNT(*) AS Cnt
FROM SiteReviews;
", conn);

            using var r = cmd.ExecuteReader();
            if (!r.Read())
                return (0.0, 0);

            double avg = r["AvgRating"] == DBNull.Value ? 0.0 : (double)r["AvgRating"];
            int cnt = r["Cnt"] == DBNull.Value ? 0 : (int)r["Cnt"];
            return (avg, cnt);
        }

        public List<SiteReviewRowVM> GetAll()
        {
            var list = new List<SiteReviewRowVM>();

            using var conn = new SqlConnection(_cs);
            conn.Open();

            using var cmd = new SqlCommand(@"
SELECT 
    sr.ReviewId,
    sr.Rating,
    sr.Comment,
    sr.CreatedAt,
    ISNULL(NULLIF(LTRIM(RTRIM(u.FirstName + ' ' + u.LastName)), ''), 'Guest') AS FullName
FROM SiteReviews sr
LEFT JOIN Users u ON u.Email = sr.UserEmail
ORDER BY sr.CreatedAt DESC;
", conn);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new SiteReviewRowVM
                {
                    ReviewId = (int)r["ReviewId"],
                    Rating = Convert.ToInt32(r["Rating"]),
                    Comment = r["Comment"].ToString() ?? "",
                    CreatedAt = (DateTime)r["CreatedAt"],
                    FullName = r["FullName"].ToString() ?? "Guest"
                });
            }

            return list;
        }

        public bool HasUserReviewed(string userEmail)
        {
            using var conn = new SqlConnection(_cs);
            conn.Open();

            using var cmd = new SqlCommand(@"
SELECT COUNT(*)
FROM SiteReviews
WHERE UserEmail = @Email;
", conn);

            cmd.Parameters.AddWithValue("@Email", userEmail);
            int cnt = (int)cmd.ExecuteScalar();
            return cnt > 0;
        }

        public void AddReview(string userEmail, int rating, string comment)
        {
            using var conn = new SqlConnection(_cs);
            conn.Open();

            using var cmd = new SqlCommand(@"
INSERT INTO SiteReviews (UserEmail, Rating, Comment)
VALUES (@Email, @Rating, @Comment);
", conn);

            cmd.Parameters.AddWithValue("@Email", userEmail);
            cmd.Parameters.AddWithValue("@Rating", rating);
            cmd.Parameters.AddWithValue("@Comment", comment);

            cmd.ExecuteNonQuery();
        }
    }
}
