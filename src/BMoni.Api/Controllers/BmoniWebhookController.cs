using BMoni.Application.Bmoni;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BMoni.Api.Controllers;

[ApiController]
public sealed class BmoniWebhookController(IBmoniWebhookService webhooks) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("/webhooks/bmoni")]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        var result = await webhooks.ReceiveAsync(buffer.ToArray(),
            Request.Headers["X-Webhook-Signature"].ToString(),
            Request.Headers["X-Webhook-Id"].ToString(), cancellationToken);
        if (!result.Accepted)
        {
            if (result.Error == "Webhook verification is not configured.")
                return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: result.Error);
            if (result.Error == "Webhook signature is invalid.") return Unauthorized(new { error = result.Error });
            return BadRequest(new { error = result.Error });
        }
        return Ok(new { received = true, duplicate = result.Duplicate });
    }
}
