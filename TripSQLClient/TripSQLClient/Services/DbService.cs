using System.Data;
using Microsoft.Data.SqlClient;
using TripSQLClient.Models;
using TripSQLClient.Models.DTOs;

namespace TripSQLClient.Services;

public interface IDbService
{
    Task<IEnumerable<TripGetDTO>> GetTripsAsync();
    Task<IEnumerable<ClientTripGetDTO>> GetClientTripsAsync(int idClient);
    Task<Client> CreateClientAsync(ClientCreateDTO clientDto);
    Task RegisterClientForTripAsync(int idClient, int idTrip, ClientTripRegisterDTO dto);
    Task<bool> DeleteClientFromTripAsync(int idClient, int idTrip);
}

public class DbService(IConfiguration config) : IDbService
{
    private readonly string? _connectionString = config.GetConnectionString("Default");
// Pobiera wszystkie wycieczki z bazy danych
    public async Task<IEnumerable<TripGetDTO>> GetTripsAsync()
    {
        var tripsDict = new Dictionary<int, TripGetDTO>();
        var countriesDict = new Dictionary<int, CountryGetDTO>();
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // zapamietuje kraje
        await using (var command = new SqlCommand("SELECT IdCountry, Name FROM Country", connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                countriesDict.Add(
                    reader.GetInt32(0),
                    new CountryGetDTO { IdCountry = reader.GetInt32(0), Name = reader.GetString(1) }
                );
            }
        }

        // zapamietuje wycieczki
        await using (var command = new SqlCommand(
            "SELECT IdTrip, Name, Description, DateFrom, DateTo, MaxPeople FROM Trip", 
            connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tripsDict.Add(reader.GetInt32(0), new TripGetDTO
                {
                    IdTrip = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    DateFrom = reader.GetDateTime(3),
                    DateTo = reader.GetDateTime(4),
                    MaxPeople = reader.GetInt32(5),
                    Countries = new List<CountryGetDTO>()
                });
            }
        }

        // mapuje kraje na wycieczki
        await using (var command = new SqlCommand(
            "SELECT IdCountry, IdTrip FROM Country_Trip", 
            connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                int countryId = reader.GetInt32(0);
                int tripId = reader.GetInt32(1);
                if (tripsDict.TryGetValue(tripId, out var trip) && countriesDict.TryGetValue(countryId, out var country))
                {
                    trip.Countries.Add(country);
                }
            }
        }

        return tripsDict.Values;
    }
// Pobiera wycieczki dla konkretnego klienta
    public async Task<IEnumerable<ClientTripGetDTO>> GetClientTripsAsync(int idClient)
    {
        var trips = new List<ClientTripGetDTO>();
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Sprawdza czy klient istnieje
        await using (var command = new SqlCommand(
            "SELECT 1 FROM Client WHERE IdClient = @idClient", 
            connection))
        {
            command.Parameters.AddWithValue("@idClient", idClient);
            if (await command.ExecuteScalarAsync() == null)
                throw new Exception($"Client with ID {idClient} not found");
        }

        // pobiera wycieczki klienta
        await using (var command = new SqlCommand(
            @"SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, 
                     ct.RegisteredAt, ct.PaymentDate
              FROM Client_Trip ct
              JOIN Trip t ON ct.IdTrip = t.IdTrip
              WHERE ct.IdClient = @idClient
              ORDER BY t.DateFrom DESC", 
            connection))
        {
            command.Parameters.AddWithValue("@idClient", idClient);
            
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                trips.Add(new ClientTripGetDTO
                {
                    IdTrip = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    DateFrom = reader.GetDateTime(3),
                    DateTo = reader.GetDateTime(4),
                    RegisteredAt = reader.GetInt32(5),
                    PaymentDate = reader.IsDBNull(6) ? null : reader.GetInt32(6)
                });
            }
        }

        return trips;
    }
//Tworzy nowego klienta w bazie danych
    public async Task<Client> CreateClientAsync(ClientCreateDTO clientDto)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            @"INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
              OUTPUT INSERTED.*
              VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel)", 
            connection);
        
        command.Parameters.AddWithValue("@FirstName", clientDto.FirstName);
        command.Parameters.AddWithValue("@LastName", clientDto.LastName);
        command.Parameters.AddWithValue("@Email", clientDto.Email);
        command.Parameters.AddWithValue("@Telephone", clientDto.Telephone);
        command.Parameters.AddWithValue("@Pesel", clientDto.Pesel);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new Exception("Failed to create client");

        return new Client
        {
            IdClient = reader.GetInt32(0),
            FirstName = reader.GetString(1),
            LastName = reader.GetString(2),
            Email = reader.GetString(3),
            Telephone = reader.GetString(4),
            Pesel = reader.GetString(5)
        };
    }
//Rejestruje klienta na wycieczkę z walidacją danych
    public async Task RegisterClientForTripAsync(int idClient, int idTrip, ClientTripRegisterDTO dto)
{
    await using var connection = new SqlConnection(_connectionString);
    await connection.OpenAsync();

    // 1. Walidacja klienta
    using (var cmd = new SqlCommand("SELECT 1 FROM Client WHERE IdClient = @idClient", connection))
    {
        cmd.Parameters.AddWithValue("@idClient", idClient);
        if (await cmd.ExecuteScalarAsync() == null)
            throw new Exception("Client not found");
    }

    // 2. Walidacja wycieczki - konwersja DATETIME na INT
    var currentDate = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
    using (var cmd = new SqlCommand(
        @"SELECT 1 FROM Trip 
          WHERE IdTrip = @idTrip 
          AND CONVERT(VARCHAR(8), DateFrom, 112) > @currentDate", // Format YYYYMMDD
        connection))
    {
        cmd.Parameters.AddWithValue("@idTrip", idTrip);
        cmd.Parameters.AddWithValue("@currentDate", currentDate);
        if (await cmd.ExecuteScalarAsync() == null)
            throw new Exception("Trip not found or already started");
    }

    //  Sprawdź duplikat
    using (var cmd = new SqlCommand(
        "SELECT 1 FROM Client_Trip WHERE IdClient = @idClient AND IdTrip = @idTrip", 
        connection))
    {
        cmd.Parameters.AddWithValue("@idClient", idClient);
        cmd.Parameters.AddWithValue("@idTrip", idTrip);
        if (await cmd.ExecuteScalarAsync() != null)
            throw new Exception("Client already registered for this trip");
    }

    //  Wstaw rejestrację
    var registeredAt = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
    using (var cmd = new SqlCommand(
        @"INSERT INTO Client_Trip 
          (IdClient, IdTrip, RegisteredAt, PaymentDate)
          VALUES (@idClient, @idTrip, @registeredAt, @paymentDate)", 
        connection))
    {
        cmd.Parameters.Add("@idClient", SqlDbType.Int).Value = idClient;
        cmd.Parameters.Add("@idTrip", SqlDbType.Int).Value = idTrip;
        cmd.Parameters.Add("@registeredAt", SqlDbType.Int).Value = registeredAt;
        
        var paymentParam = cmd.Parameters.Add("@paymentDate", SqlDbType.Int);
        paymentParam.Value = dto.PaymentDate ?? (object)DBNull.Value;
        
        await cmd.ExecuteNonQueryAsync();
    }
}
//Usuwa rejestrację klienta z wycieczki
    public async Task<bool> DeleteClientFromTripAsync(int idClient, int idTrip)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Sprawdza czy istnieje rejestracja
        await using (var command = new SqlCommand(
            "SELECT 1 FROM Client_Trip WHERE IdClient = @idClient AND IdTrip = @idTrip", 
            connection))
        {
            command.Parameters.AddWithValue("@idClient", idClient);
            command.Parameters.AddWithValue("@idTrip", idTrip);
            if (await command.ExecuteScalarAsync() == null)
                return false;
        }

        // usuwa rejestracje
        await using (var command = new SqlCommand(
            "DELETE FROM Client_Trip WHERE IdClient = @idClient AND IdTrip = @idTrip", 
            connection))
        {
            command.Parameters.AddWithValue("@idClient", idClient);
            command.Parameters.AddWithValue("@idTrip", idTrip);
            return await command.ExecuteNonQueryAsync() > 0;
        }
    }
}