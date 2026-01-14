using TripsProject.Models;
using System.Collections.Generic;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace TripsProject.Data;

public class PackageRepository
{
    private readonly string _connectionString;

    public PackageRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TravelDb");
    }

    public List<Package> GetAllPackages()
    {
        var list = new List<Package>();
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand("SELECT * FROM TravelPackages", conn);
            var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Package
                {
                    PackageId = (int)reader["PackageId"],
                    Destination = reader["Destination"].ToString(),
                    Country = reader["Country"].ToString(),
                    StartDate = (DateTime)reader["StartDate"],
                    EndDate = (DateTime)reader["EndDate"],
                    Price = (decimal)reader["Price"],
                    NumOfPeople = (int)reader["NumOfPeople"],
                    PackageType = reader["PackageType"].ToString(),
                    AgeLimit = (int)reader["AgeLimit"],
                    Description = reader["Description"].ToString(),
                    IsAvailable = (bool)reader["IsAvailable"],
                    Amount = (int)reader["Amount"],
                    TrackDesc =   reader["TrackDesc"].ToString()
                });
            }
        }

        return list;
    }

    public void AddPackage(Package package)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand(
                "INSERT INTO TravelPackages (Destination, Country, StartDate, EndDate, Price, NumOfPeople, PackageType, AgeLimit, Description, IsAvailable,  Amount,TrackDesc) " +
                "VALUES (@Destination, @Country, @StartDate, @EndDate, @Price, @NumOfPeople, @PackageType, @AgeLimit, @Description, @IsAvailable, @Amount,@TrackDesc)",
                conn);

            cmd.Parameters.AddWithValue("@Destination", package.Destination);
            cmd.Parameters.AddWithValue("@Country", package.Country);
            cmd.Parameters.AddWithValue("@StartDate", package.StartDate);
            cmd.Parameters.AddWithValue("@EndDate", package.EndDate);
            cmd.Parameters.AddWithValue("@Price", package.Price);
            cmd.Parameters.AddWithValue("@NumOfPeople", package.NumOfPeople);
            cmd.Parameters.AddWithValue("@PackageType", package.PackageType);
            cmd.Parameters.AddWithValue("@AgeLimit", package.AgeLimit);
            cmd.Parameters.AddWithValue("@Description", package.Description);
            cmd.Parameters.AddWithValue("@IsAvailable", package.IsAvailable);
            cmd.Parameters.AddWithValue("@Amount", package.Amount);
            cmd.Parameters.AddWithValue("@TrackDesc", package.TrackDesc);

            cmd.ExecuteNonQuery();
        }
    }


    public Package GetPackageById(int packageId)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand("SELECT * FROM TravelPackages WHERE PackageId = @PackageId", conn);
            cmd.Parameters.AddWithValue("@PackageId", packageId);

            var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Package
                {
                    PackageId = (int)reader["PackageId"],
                    Destination = reader["Destination"].ToString(),
                    Country = reader["Country"].ToString(),
                    StartDate = (DateTime)reader["StartDate"],
                    EndDate = (DateTime)reader["EndDate"],
                    Price = (decimal)reader["Price"],
                    NumOfPeople = (int)reader["NumOfPeople"],
                    PackageType = reader["PackageType"].ToString(),
                    AgeLimit = (int)reader["AgeLimit"],
                    Description = reader["Description"].ToString(),
                    IsAvailable = (bool)reader["IsAvailable"],
                    Amount = (int)reader["Amount"],
                    TrackDesc =   reader["TrackDesc"].ToString()
                };
            }
        }
        return null; 
    }
    
    public void UpdatePackage(Package package)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand(@"
            UPDATE TravelPackages
            SET Destination = @Destination,
                Country = @Country,
                StartDate = @StartDate,
                EndDate = @EndDate,
                Price = @Price,
                NumOfPeople = @NumOfPeople,
                PackageType = @PackageType,
                AgeLimit = @AgeLimit,
                Description = @Description,
                IsAvailable = @IsAvailable,
                Amount = @Amount,
                TrackDesc  = @TrackDesc
            
            WHERE PackageId = @PackageId
        ", conn);

            cmd.Parameters.AddWithValue("@PackageId", package.PackageId);
            cmd.Parameters.AddWithValue("@Destination", package.Destination);
            cmd.Parameters.AddWithValue("@Country", package.Country);
            cmd.Parameters.AddWithValue("@StartDate", package.StartDate);
            cmd.Parameters.AddWithValue("@EndDate", package.EndDate);
            cmd.Parameters.AddWithValue("@Price", package.Price);
            cmd.Parameters.AddWithValue("@NumOfPeople", package.NumOfPeople);
            cmd.Parameters.AddWithValue("@PackageType", package.PackageType);
            cmd.Parameters.AddWithValue("@AgeLimit", package.AgeLimit);
            cmd.Parameters.AddWithValue("@Description", package.Description);
            cmd.Parameters.AddWithValue("@IsAvailable", package.IsAvailable);
            cmd.Parameters.AddWithValue("@Amount", package.Amount);
            cmd.Parameters.AddWithValue("@TrackDesc", package.TrackDesc);

            cmd.ExecuteNonQuery();
        }
    }
    
    public void DeletePackage(int packageId)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            var cmd = new SqlCommand(
                "DELETE FROM TravelPackages WHERE PackageId = @PackageId", conn);

            cmd.Parameters.AddWithValue("@PackageId", packageId);
            cmd.ExecuteNonQuery();
        }
    }
}



