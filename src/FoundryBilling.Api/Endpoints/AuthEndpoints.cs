using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FoundryBilling.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth")
            .WithTags("Auth");

        auth.MapGet("/login", Login)
            .WithName("Login")
            .WithSummary("Starts the Microsoft Entra sign-in flow.");

        auth.MapGet("/logout", Logout)
            .WithName("Logout")
            .WithSummary("Signs the current user out and returns to the app.");

        auth.MapGet("/me", Me)
            .WithName("GetCurrentUser")
            .WithSummary("Gets the current signed-in user.");

        return app;
    }

    private static IResult Login(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated == true)
        {
            return TypedResults.LocalRedirect("/");
        }

        return TypedResults.Challenge(new AuthenticationProperties
        {
            RedirectUri = "/"
        });
    }

    private static IResult Logout(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return TypedResults.LocalRedirect("/");
        }

        return TypedResults.SignOut(
            new AuthenticationProperties
            {
                RedirectUri = "/"
            },
            [
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme
            ]);
    }

    private static Results<Ok<AuthMeResponse>, UnauthorizedHttpResult> Me(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return TypedResults.Unauthorized();
        }

        var email = user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("preferred_username")
            ?? user.FindFirstValue(ClaimTypes.Upn)
            ?? user.FindFirstValue("upn")
            ?? string.Empty;
        var name = user.Identity.Name
            ?? user.FindFirstValue(ClaimTypes.Name)
            ?? user.FindFirstValue("name")
            ?? email;

        return TypedResults.Ok(new AuthMeResponse(name, email));
    }

    private sealed record AuthMeResponse(string Name, string Email);
}
