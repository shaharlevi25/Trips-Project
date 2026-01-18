using TripsProject.Models;
using System.Collections.Generic;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace TripsProject.Data
{
    public class UserRepository
    {
        private readonly string _connectionString;

        public UserRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("TravelDb"); 
        }

        
        public List<User> GetAllUsers()
        {
            var list = new List<User>();
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM [Users]", conn);
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new User
                    {
                        Email = reader["Email"].ToString(),
                        FirstName = reader["FirstName"].ToString(),
                        LastName = reader["LastName"].ToString(),
                        PhoneNumber = reader["PhoneNumber"].ToString(),
                        Password = reader["Password"].ToString(),
                        Role = reader["Role"].ToString(),
                        IsActive = (bool)reader["IsActive"]
                    });
                }
            }
            return list;
        }

        
        public User GetUserByEmail(string email)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM [Users] WHERE Email = @Email", conn);
                cmd.Parameters.AddWithValue("@Email", email);

                var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new User
                    {
                        Email = reader["Email"].ToString(),
                        FirstName = reader["FirstName"].ToString(),
                        LastName = reader["LastName"].ToString(),
                        PhoneNumber = reader["PhoneNumber"].ToString(),
                        Password = reader["Password"].ToString(),
                        Role = reader["Role"].ToString(),
                        IsActive = (bool)reader["IsActive"]
                    };
                }
            }
            return null;
        }

        
        public void AddUser(User user)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    INSERT INTO [Users] 
                    (Email, FirstName, LastName, PhoneNumber, Password, Role, IsActive)
                    VALUES (@Email, @FirstName, @LastName, @PhoneNumber, @Password, @Role, @IsActive)
                ", conn);

                cmd.Parameters.AddWithValue("@Email", user.Email);
                cmd.Parameters.AddWithValue("@FirstName", user.FirstName);
                cmd.Parameters.AddWithValue("@LastName", user.LastName);
                cmd.Parameters.AddWithValue("@PhoneNumber", user.PhoneNumber);
                cmd.Parameters.AddWithValue("@Password", user.Password);
                cmd.Parameters.AddWithValue("@Role", user.Role ?? "User");
                cmd.Parameters.AddWithValue("@IsActive", user.IsActive);

                cmd.ExecuteNonQuery();
            }
        }

        
        public void UpdateUser(User user)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    UPDATE [User]
                    SET FirstName = @FirstName,
                        LastName = @LastName,
                        PhoneNumber = @PhoneNumber,
                        Password = @Password,
                        Role = @Role,
                        IsActive = @IsActive
                    WHERE Email = @Email
                ", conn);

                cmd.Parameters.AddWithValue("@Email", user.Email);
                cmd.Parameters.AddWithValue("@FirstName", user.FirstName);
                cmd.Parameters.AddWithValue("@LastName", user.LastName);
                cmd.Parameters.AddWithValue("@PhoneNumber", user.PhoneNumber);
                cmd.Parameters.AddWithValue("@Password", user.Password);
                cmd.Parameters.AddWithValue("@Role", user.Role ?? "User");
                cmd.Parameters.AddWithValue("@IsActive", user.IsActive);

                cmd.ExecuteNonQuery();
            }
        }

        
        public void DeleteUser(string email)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("DELETE FROM [Users] WHERE Email = @Email", conn);
                cmd.Parameters.AddWithValue("@Email", email);
                cmd.ExecuteNonQuery();
            }
        }
        
    }
}
