using Microsoft.AspNetCore.Mvc;
using TripSQLClient.Models.DTOs;
using TripSQLClient.Services;

namespace TripSQLClient.Controllers;
[ApiController]
[Route("api/[controller]")]
public class ClientsController(IDbService dbService) : ControllerBase
{
    [HttpGet("{id}/trips")]
    public async Task<IActionResult> GetClientTrips(int id)
    {
        try
        {
            return Ok(await dbService.GetClientTripsAsync(id));
        }
        catch (Exception ex)
        {
            return NotFound(ex.Message);
        }
    }
    
    
    [HttpPost]
    public async Task<IActionResult> CreateClient([FromBody] ClientCreateDTO body)
    {
        var client = await dbService.CreateClientAsync(body);
        return Created($"/api/clients/{client.IdClient}", client.IdClient);
        
        
    }
    
    [HttpPut("{idClient}/trips/{idTrip}")]
    public async Task<IActionResult> RegisterForTrip(
        int idClient, 
        int idTrip,
        [FromBody] ClientTripRegisterDTO body)
    {
        try
        {
            await dbService.RegisterClientForTripAsync(idClient, idTrip, body);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    [HttpDelete("{idClient}/trips/{idTrip}")]
    public async Task<IActionResult> DeleteClientFromTrip(int idClient, int idTrip)
    {
        try
        {
            bool deleted = await dbService.DeleteClientFromTripAsync(idClient, idTrip);
            return deleted ? NoContent() : NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
}