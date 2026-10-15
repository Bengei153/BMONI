using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BMoni.Application.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BMoni.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return StatusCode(StatusCodes.Status201Created,
                await sender.Send(new RegisterUserCommand(request.Email, request.FirstName, request.Password, request.Roles), cancellationToken));
        }
        catch (DuplicateEmailException)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Email already registered.");
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await sender.Send(new LoginUserCommand(request.Email, request.Password), cancellationToken);
        return response is null ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Email or password is incorrect.") : Ok(response);
    }

    [Authorize]
    [HttpGet("/api/me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var idValue = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idValue, out var id)) return Unauthorized();
        var user = await sender.Send(new GetCurrentUserQuery(id), cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }
}
