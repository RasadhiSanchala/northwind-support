using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.Presentation.Auth;

public sealed record AgentUserDto(int Id, string FullName, string Email);
public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, AgentUserDto User);

public sealed class JwtTokenService(IConfiguration configuration)
{
    public LoginResponse Create(AuthenticatedAgent agent)
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT signing key is missing.");
        var issuer = configuration["Jwt:Issuer"] ?? "NorthwindSupport";
        var audience = configuration["Jwt:Audience"] ?? "NorthwindSupportWeb";
        var expiryMinutes = int.TryParse(configuration["Jwt:LifetimeMinutes"], out var n) ? n : 60;
        if (expiryMinutes is < 1 or > 1440)
            throw new InvalidOperationException("JWT lifetime must be 1–1440 minutes.");
        var expiry = DateTime.UtcNow.AddMinutes(expiryMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, agent.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, agent.FullName),
            new Claim(JwtRegisteredClaimNames.Email, agent.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims,
            expires: expiry, signingCredentials: credentials);
        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expiry,
            new AgentUserDto(agent.Id, agent.FullName, agent.Email));
    }
}
