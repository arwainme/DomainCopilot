using DomainCopilot.API.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly JwtAuthenticationService _authenticationService;

    public AuthController(
        JwtAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var role = request.Username.ToLowerInvariant() switch
        {
            "citizen" when request.Password == "citizen123"
                => "Citizen",

            "officer" when request.Password == "officer123"
                => "Officer",

            _ => null
        };

        if (role is null)
        {
            return Unauthorized(new
            {
                message = "Invalid username or password."
            });
        }

        var token = _authenticationService
            .GenerateToken(request.Username, role);

        return Ok(new
        {
            accessToken = token,
            username = request.Username,
            role
        });
    }

    public sealed record LoginRequest(
        string Username,
        string Password);
}