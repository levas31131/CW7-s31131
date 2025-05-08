using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class tripsController: ControllerBase
{
    
    //pobiera zapytanie do wyswietlenia wszystkich dostępnych wycieczki wraz z krajami
    [HttpGet]
    public IActionResult GetTrips()
    {

        try
        {
            var trips = new List<Trip>();
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            var connectionString = config["ConnectionStrings:DB_kerberos"];
            using (var con = new SqlConnection(connectionString))
            {
                //wyswietlenie wszystkich dostępnych wycieczki wraz z krajami
                var com = new SqlCommand(@"
            SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople, c.Name AS CountryName
            FROM Trip t
            JOIN Country_Trip ct ON t.IdTrip = ct.IdTrip
            JOIN Country c ON c.IdCountry = ct.IdCountry
        ", con);

                con.Open();
                using (var reader = com.ExecuteReader())
                {
                    var tripDict = new Dictionary<int, Trip>();
                    while (reader.Read())
                    {
                        var idTrip = reader.GetInt32(0);
                        if (!tripDict.ContainsKey(idTrip))
                        {
                            tripDict[idTrip] = new Trip
                            {
                                IdTrip = idTrip,
                                Name = reader.GetString(1),
                                Description = reader.GetString(2),
                                DateFrom = reader.GetDateTime(3),
                                DateTo = reader.GetDateTime(4),
                                MaxPeople = reader.GetInt32(5),
                                Countries = new List<string>()
                            };
                        }

                        tripDict[idTrip].Countries.Add(reader.GetString(6));
                    }

                    trips = tripDict.Values.ToList();
                }


                return Ok(trips);
            }
        }
        catch (Exception e)
        {
            return StatusCode(500, $"Error: {e.Message}");
        }

    }
}