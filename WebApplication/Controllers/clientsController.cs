using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class clientsController : ControllerBase
{
    
    //pobiera zapytanie do wyswietlenia wszystkich wycieczki klienta z podanym id
    [HttpGet("{id}/trips")]
    public IActionResult GetClientTrips(int id)
    {
        try
        {
            var trips = new List<ClientTrip>();
            using (var con = GetConnection())
            {
                con.Open();
                
                //sprawdza czy jest klient z podanym id
                var checkCom = new SqlCommand("SELECT 1 FROM Client WHERE IdClient = @IdClient", con);
                checkCom.Parameters.AddWithValue("@IdClient", id);
                if (checkCom.ExecuteScalar() == null)
                    return NotFound($"Client with ID {id} not found.");
                
                //wybiera wszystkie wycieczki klienta
                var command = new SqlCommand(@"
                SELECT 
                    t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                    ct.RegisteredAt, ct.PaymentDate
                FROM Client_Trip ct
                JOIN Trip t ON ct.IdTrip = t.IdTrip
                WHERE ct.IdClient = @IdClient", con);
                
                command.Parameters.AddWithValue("@IdClient", id);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    trips.Add(new ClientTrip
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Description = reader.GetString(2),
                        DateFrom = reader.GetDateTime(3),
                        DateTo = reader.GetDateTime(4),
                        MaxPeople = reader.GetInt32(5),
                        RegisteredAt = reader.GetInt32(6),
                        PaymentDate = reader.IsDBNull(7) ? null : reader.GetInt32(7)
                    });
                }

                return Ok(trips);
            }
        }
        catch (Exception e)
        {
            return StatusCode(500, $"Error: {e.Message}");
        }
    }
    
    //pobiera zapytanie do dodania nowego klienta
    [HttpPost]
    public IActionResult AddClient([FromBody] Client client)
    {
        if (client == null
            || client.FirstName.IsNullOrEmpty()
            || client.LastName.IsNullOrEmpty()
            || client.Email.IsNullOrEmpty()
            || client.Telephone.IsNullOrEmpty()
            || client.Pesel.IsNullOrEmpty()
            || client.Pesel.Length != 11)
            return BadRequest("Please fill all the fields.");
        
        try
        {
            using var con = GetConnection();
            con.Open();
            
            //dodaje nowego klienta
            var com = new SqlCommand(@"INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
                OUTPUT INSERTED.IdClient
                VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel)", con);
            
            com.Parameters.AddWithValue("@FirstName", client.FirstName);
            com.Parameters.AddWithValue("@LastName", client.LastName);
            com.Parameters.AddWithValue("@Email", client.Email);
            com.Parameters.AddWithValue("@Telephone", client.Telephone);
            com.Parameters.AddWithValue("@Pesel", client.Pesel);
            var clientId = com.ExecuteScalar();
            return Ok("New id: " + clientId);
        }
        catch (Exception e)
        {
            return StatusCode(500, $"Error: {e.Message}");
        }
    }



    //pobiera zapytanie do dodawania nowej rezerwacji 
    [HttpPut("{id}/trips/{tripId}")]
    public IActionResult PutClientTrip(int id, int tripId)
    {
        try
        {
            using var con = GetConnection();
            con.Open();
            var transaction = con.BeginTransaction();

            //sprawdza czy jest podany klient 
            var checkCom = new SqlCommand("SELECT 1 FROM Client WHERE IdClient = @IdClient", con, transaction);
            checkCom.Parameters.AddWithValue("@IdClient", id);
            if (checkCom.ExecuteScalar() == null)
                return NotFound($"Client with ID {id} not found.");
            //sprawdza czy jest podana wycieczka
            var checkTripCom = new SqlCommand("SELECT 1 FROM Trip WHERE IdTrip = @IdTrip", con, transaction);
            checkTripCom.Parameters.AddWithValue("@IdTrip", tripId);
            if (checkTripCom.ExecuteScalar() == null)
                return NotFound($"Trip with ID {id} not found.");

            //Sprawdzenie maksymalnej liczby uczestników wycieczki
            var checkPeopleCom =
                new SqlCommand(@"SELECT MAXPEOPLE FROM Trip WHERE IdTrip = @IdTrip", con, transaction);
            checkPeopleCom.Parameters.AddWithValue("@IdTrip", tripId);
            var maxPeople = checkPeopleCom.ExecuteScalar();
            
            //Sprawdzenie aktualnej liczby uczestników wycieczki
            var countCom = new SqlCommand("SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @IdTrip", con,
                transaction);
            countCom.Parameters.AddWithValue("@IdTrip", tripId);
            int regClients = (int)countCom.ExecuteScalar();
            if (regClients + 1 > Convert.ToInt32(maxPeople))
                return BadRequest("Maximum people reached");
            
            //Dodanie rejestracji klienta na wycieczkę
            var com = new SqlCommand(@"
            INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt)
            VALUES (@IdClient, @IdTrip, @RegisteredAt)", con, transaction);
            com.Parameters.AddWithValue("@IdClient", id);
            com.Parameters.AddWithValue("@IdTrip", tripId);
            
            int now = DateToInt(DateTime.Now);
            com.Parameters.AddWithValue("@RegisteredAt", now);

            com.ExecuteNonQuery();
            transaction.Commit();
            return Ok("Succesfully registered trip.");
        }
        catch (Exception e)
        {
            return StatusCode(500, $"Error: {e.Message}");
            
        }
    }

    //podanie zapytania do usunięcia rejestracji
    [HttpDelete("{id}/trips/{tripId}")]
    public IActionResult DeleteClientTrip(int id, int tripId)
    {
        try
        {
            using var con = GetConnection();
            con.Open();

            //sprawdzanie czy jest wycieczka z podan klientem i wycieczką
            var checkCom =
                new SqlCommand("Select 1 from Client_Trip where IdClient = @IdClient and IdTrip = @IdTrip", con);
            checkCom.Parameters.AddWithValue("@IdTrip", tripId);
            checkCom.Parameters.AddWithValue("@IdClient", id);

            if (checkCom.ExecuteScalar() == null)
                return NotFound($"Client with ID {id} or trip with ID {tripId} not found.");

            //usunięcie rejestracji ppodanego klienta i podanej wycieczki 
            var deleteCom =
                new SqlCommand("DELETE FROM Client_Trip WHERE IdClient = @IdClient and IdTrip = @IdTrip", con);
            deleteCom.Parameters.AddWithValue("@IdClient", id);
            deleteCom.Parameters.AddWithValue("@IdTrip", tripId);
            deleteCom.ExecuteNonQuery();

            return Ok("Succesfully deleted trip.");
        }
        catch (Exception e)
        {
            return StatusCode(500, $"Error: {e.Message}");
        }
    }
    
    private static SqlConnection GetConnection()
    {
        SqlConnection? con = null;
        try
        {
            var connectionString = Configuratin["ConnectionStrings:DB_kerberos"];
            con = new SqlConnection(connectionString);
            return con;
        }
        catch
        {
            con?.Dispose();
            throw;
        }
    }
    private static IConfigurationRoot Configuratin
    {
        get
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            return config;
        }
    }
    private static int DateToInt(DateTime now)
    {
        return int.Parse("" + now.Year + (now.Month < 10 ? "0" + now.Month : "" + now.Month) +
                         (now.Day < 10 ? "0" + now.Day : "" + now.Day));
    }
}