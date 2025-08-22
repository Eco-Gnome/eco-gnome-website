using System.Text.Json;
using ecocraft.Models;
using ecocraft.Services.DbServices;
using ecocraft.Services.ImportData;
using Microsoft.AspNetCore.Mvc;
using StableNameDotNet;

namespace ecocraft.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServerController(
    ServerDbService serverDbService,
    ImportDataService importDataService
) : ControllerBase
{
    [HttpPost("upload-data")]
    public async Task<IActionResult> UploadData([FromQuery] string apiKey, [FromBody] JsonElement jsonContent)
    { 
        var result = await TryGetServerByApiKey(apiKey);
        if (result.Result is not null) return result.Result;
        var server = result.Value!;

        try
        {
            var (errorCount, recipeErrorNames) = await importDataService.ImportServerData(jsonContent.ToString(), server);

            if (errorCount == 0)
            {
                return Ok("Import ok");
            }
            
            return Ok($"Import successful but with warning: {errorCount} recipes have not been imported: " + recipeErrorNames.Join(","));   
        }
        catch (ImportException ex)
        {
            Console.WriteLine(ex);
            return BadRequest("Upload Error:" + ex.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return BadRequest("UNEXPECTED_ERROR: " + ex.Message);
        }
    }
    
    private async Task<ActionResult<Server>> TryGetServerByApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return BadRequest("apiKey is required and cannot be empty.");

        var server = await serverDbService.GetByApiKeyAsync(new Guid(apiKey));
        if (server is null)
            return BadRequest("Can't find server.");

        return server;
    }
}