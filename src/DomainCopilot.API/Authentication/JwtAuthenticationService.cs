using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DomainCopilot.API.Authentication;

public sealed class JwtAuthenticationService
{
    private const string SecretKey =
        "DomainCopilot-Demo-Secret-Key-2026-Change-In-Production";

    public string GenerateToken(string username, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(SecretKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}