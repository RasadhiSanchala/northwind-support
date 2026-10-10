using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Infrastructure.Services;
using SupportDesk.Presentation.Auth;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private static readonly string LoginFailure = "Invalid email or password.";

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        [FromServices] AgentAuthenticationService authentication,
        [FromServices] JwtTokenService tokens,
        CancellationToken ct)
    {
        var agent = await authentication.AuthenticateAsync(request.Email, request.Password, ct);
        if (agent is null) return Unauthorized(new ProblemDetails
        {
            Title = "Unauthorized", Detail = LoginFailure, Status = StatusCodes.Status401Unauthorized
        });
        return Ok(tokens.Create(agent));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AgentUserDto>> Me(
        [FromServices] AgentAuthenticationService authentication,
        CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id))
            return Unauthorized();
        var agent = await authentication.GetActiveAgentAsync(id, ct);
        if (agent is null) return Unauthorized();
        return Ok(new AgentUserDto(agent.Id, agent.FullName, agent.Email));
    }
}
