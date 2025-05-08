using Microsoft.AspNetCore.Mvc;
using TripSQLClient.Services;

namespace TripSQLClient.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TripsController(IDbService dbService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTrips()
    {
        
            return Ok(await dbService.GetTripsAsync());
        
    }
    
   
}