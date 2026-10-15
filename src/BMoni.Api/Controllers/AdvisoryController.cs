using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BMoni.Application.Loans;
using BMoni.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BMoni.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class AdvisoryController(IPlatformService platform, IAdvisoryService advisory) : ControllerBase
{
    [HttpPost("scam-check")]
    [EnableRateLimiting("advisory")]
    [ProducesResponseType<ScamCheckResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ScamCheck([FromBody] ScamCheckRequest request, CancellationToken cancellationToken)
    {
        return Ok(await advisory.CheckScamAsync(CurrentUserId(), request, cancellationToken));
    }

    [HttpPost("assistant/chat")]
    [EnableRateLimiting("advisory")]
    [ProducesResponseType<AssistantChatResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Chat([FromBody] AssistantChatRequest request, CancellationToken cancellationToken)
    {
        var facts = await platform.GetDashboardAsync(CurrentUserId(), cancellationToken);
        return Ok(await advisory.ChatAsync(facts, request.Question, cancellationToken));
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(value, out var id)) return id;
        throw new UnauthorizedAccessException("The access token does not contain a valid user id.");
    }
}
