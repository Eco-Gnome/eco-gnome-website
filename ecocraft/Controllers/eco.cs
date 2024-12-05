using ecocraft.Models;
using ecocraft.Services.DbServices;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ecocraft.Controllers;

[ApiController]
[Microsoft.AspNetCore.Mvc.Route("api/[controller]")]
public class EcoController(EcoCraftDbContext dbContext, UserPriceDbService userPriceDbService, ServerDbService serverDbService, UserDbService userDbService) : ControllerBase
{
    /*
     * Registrations:
     *   Concept: The Mod does not know about the registration process. The linking data is done on eco-gnome side.
     *   The Mod will only communicate with its ecoServerId and its ecoUserId
     *
     *   - Server Admin registers his server & himself
     *     - type command /eco-gnome register-server joinCode, userSecretId
     *     - the mod calls eco-gnome with /api/eco/register-server joinCode, ecoServerId
     *       - if the server is not found, throw error
     *       - if the server is already associated, throw error (TODO: add button in server management to dissociate)
     *       - otherwise, save the ecoServerId in the Server
     *
     *   - Player registers his userId:
     *     - User type command /eco-gnome register <userSecretId>
     *     - The mod calls eco-gnome with /api/eco/register-user ecoServerId, userSecretId, ecoUserId
     *       - if the server is not found, throw error
     *       - if the user is not found, throw error
     *       - if the userServer does not exist, throw error (TODO: allow to join the server automatically)
     *       - if the userServer is already associated to another ecoUserId, throw error (TODO: add button in user management to dissociate)
     *       - otherwise, save the ecoUserId in the UserServer
     */

    [HttpGet("register-server")]
    public async Task<IActionResult> RegisterServer([FromQuery] string joinCode, [FromQuery] string ecoServerId)
    {
        var server = await serverDbService.GetByJoinCodeAsync(joinCode);

        if (server == null)
        {
            return NotFound("Eco-Gnome server not found. Please create your server in Eco-Gnome and retrieve the correct server id from server-management page.");
        }

        if (server.EcoServerId != ecoServerId)
        {
            return BadRequest();
        }

        server.EcoServerId = ecoServerId;

        await dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpGet("register-user")]
    public async Task<IActionResult> RegisterUser([FromQuery] string ecoServerId, [FromQuery] string userSecretId, [FromQuery] string ecoUserId, [FromQuery] string serverPseudo)
    {
        var server = await serverDbService.GetByEcoServerIdAsync(ecoServerId);

        if (server == null)
        {
            return NotFound("Eco-Gnome Server not found. Please ask the admin to register the server first.");
        }

        var user = await userDbService.GetBySecretIdAsync(new Guid(userSecretId));

        if (user == null)
        {
            return NotFound("Eco-Gnome User not found");
        }

        var userServer = user.UserServers.Find(us => us.Server.EcoServerId == ecoServerId);

        if (userServer == null)
        {
            return NotFound("Please join the server first on Eco-Gnome thanks to the JoinCode provided by your server admin.");
        }

        if (userServer.EcoUserId != ecoUserId)
        {
            return BadRequest("Eco-Gnome User is already associated to an Eco user.");
        }

        userServer.EcoUserId = ecoUserId;
        userServer.Pseudo = serverPseudo;

        await dbContext.SaveChangesAsync();

        return Ok();
    }


    [HttpGet("user-prices")]
    public async Task<IActionResult> GetUserPrices([FromQuery] string serverId, [FromQuery] string ecoUserId)
    {
        if (string.IsNullOrWhiteSpace(serverId) || string.IsNullOrWhiteSpace(ecoUserId))
        {
            return BadRequest(new { Error = "serverId and ecoUserId are required and cannot be empty." });
        }

        var userPrices = await userPriceDbService.GetByServerIdAndEcoUserId(new Guid(serverId), ecoUserId);

        return Ok(userPrices);
    }

}
