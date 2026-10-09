using AspNet.Security.OAuth.Discord;
using ecocraft.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace ecocraft.Controllers;

// Allers-retours HTTP de la connexion Discord : Blazor Server ne peut pas poser de cookie depuis un circuit, d'où des
// URL classiques ouvertes en navigation complète (forceLoad). Le rattachement au compte se fait ensuite dans le circuit,
// qui connaît le compte anonyme du localStorage (ContextService.InitializeUserContext).
[Route("auth")]
public class AuthController(IConfiguration configuration) : Controller
{
    [HttpGet("discord/login")]
    public IActionResult DiscordLogin([FromQuery] string? returnUrl)
    {
        if (!DiscordAuth.IsConfigured(configuration)) return Redirect("/");
        return Challenge(new AuthenticationProperties { RedirectUri = LocalUrl(returnUrl) }, DiscordAuthenticationDefaults.AuthenticationScheme);
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout([FromQuery] string? returnUrl)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect(LocalUrl(returnUrl));
    }

    // Jamais de redirection hors du site : returnUrl vient de la barre d'adresse.
    string LocalUrl(string? returnUrl) => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
}
