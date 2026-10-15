using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BMoni.Application.Loans;
using BMoni.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BMoni.Api.Controllers;

[ApiController]
public sealed class PlatformController(IPlatformService platform) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("api/bootstrap")]
    public async Task<IActionResult> Bootstrap(CancellationToken cancellationToken) =>
        Ok(await platform.BootstrapAsync(cancellationToken));

    [Authorize]
    [HttpGet("api/dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken) =>
        Ok(await platform.GetDashboardAsync(CurrentUserId(), cancellationToken));

    [Authorize]
    [HttpGet("api/transactions")]
    public async Task<IActionResult> Transactions([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] string? type, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await platform.GetTransactionsAsync(CurrentUserId(), from, to, type, page, pageSize, cancellationToken));

    [Authorize(Roles = nameof(UserRole.Lender))]
    [HttpGet("api/lender/returns")]
    public async Task<IActionResult> LenderReturns(CancellationToken cancellationToken) =>
        Ok(await platform.GetLenderReturnsAsync(CurrentUserId(), cancellationToken));

    [Authorize]
    [HttpGet("api/wallet")]
    public async Task<IActionResult> Wallet(CancellationToken cancellationToken) =>
        Ok(await platform.GetWalletAsync(CurrentUserId(), cancellationToken));

    [Authorize]
    [HttpGet("api/score")]
    public async Task<IActionResult> Score(CancellationToken cancellationToken) =>
        Ok(await platform.GetLatestScoreAsync(CurrentUserId(), cancellationToken));

    [Authorize]
    [HttpPost("api/wallet/deposit")]
    public async Task<IActionResult> Deposit([FromBody] DepositRequest request, CancellationToken cancellationToken)
    {
        if (!TryIdempotencyKey(out var key)) return MissingIdempotencyKey();
        return Ok(await platform.DepositAsync(CurrentUserId(), request.Amount, key, cancellationToken));
    }

    [Authorize]
    [HttpPost("api/wallet/spends")]
    public async Task<IActionResult> RequestSpend([FromBody] WalletSpendRequest request, CancellationToken cancellationToken)
    {
        if (!TryIdempotencyKey(out var key)) return MissingIdempotencyKey();
        var approval = await platform.RequestWalletSpendAsync(CurrentUserId(), request.Amount, request.Description, key, cancellationToken);
        return Accepted($"/api/approvals/{approval.Id}", approval);
    }

    [Authorize(Roles = nameof(UserRole.Borrower))]
    [HttpPost("api/loans")]
    public async Task<IActionResult> CreateLoan([FromBody] CreateLoanRequest request, CancellationToken cancellationToken)
    {
        var loan = await platform.CreateLoanAsync(CurrentUserId(), request, cancellationToken);
        return loan.Status == "Declined" ? UnprocessableEntity(loan) : Created($"/api/loans/{loan.Id}", loan);
    }

    [Authorize]
    [HttpGet("api/loans")]
    public async Task<IActionResult> Loans([FromQuery] string? role, CancellationToken cancellationToken) =>
        Ok(await platform.GetLoansAsync(CurrentUserId(), role, cancellationToken));

    [Authorize]
    [HttpGet("api/loans/{loanId:guid}")]
    public async Task<IActionResult> Loan(Guid loanId, CancellationToken cancellationToken) =>
        Ok(await platform.GetLoanAsync(CurrentUserId(), loanId, cancellationToken));

    [Authorize]
    [HttpGet("api/loans/{loanId:guid}/schedule")]
    public async Task<IActionResult> LoanSchedule(Guid loanId, CancellationToken cancellationToken) =>
        Ok((await platform.GetLoanAsync(CurrentUserId(), loanId, cancellationToken)).Schedule);

    [Authorize(Roles = nameof(UserRole.Borrower))]
    [HttpPost("api/loans/{loanId:guid}/assess")]
    public async Task<IActionResult> AssessLoan(Guid loanId, CancellationToken cancellationToken) =>
        Ok(await platform.GetLoanAssessmentAsync(CurrentUserId(), loanId, cancellationToken));

    [Authorize(Roles = nameof(UserRole.Lender))]
    [HttpPost("api/loans/{loanId:guid}/contributions")]
    public async Task<IActionResult> Contribute(Guid loanId, [FromBody] ContributeRequest request, CancellationToken cancellationToken)
    {
        if (!TryIdempotencyKey(out var key)) return MissingIdempotencyKey();
        return Ok(await platform.ContributeAsync(CurrentUserId(), loanId, request.Amount, request.AcknowledgedTerms, key, cancellationToken));
    }

    [Authorize(Roles = nameof(UserRole.Lender))]
    [HttpDelete("api/loans/{loanId:guid}/contributions/{contributionId:guid}")]
    public async Task<IActionResult> Withdraw(Guid loanId, Guid contributionId, CancellationToken cancellationToken)
    {
        if (!TryIdempotencyKey(out var key)) return MissingIdempotencyKey();
        return Ok(await platform.WithdrawContributionAsync(CurrentUserId(), loanId, contributionId, key, cancellationToken));
    }

    [Authorize(Roles = nameof(UserRole.Borrower))]
    [HttpPost("api/loans/{loanId:guid}/accept-terms")]
    public async Task<IActionResult> AcceptTerms(Guid loanId, CancellationToken cancellationToken)
    {
        if (!TryIdempotencyKey(out var key)) return MissingIdempotencyKey();
        return Ok(await platform.AcceptTermsAsync(CurrentUserId(), loanId, key, cancellationToken));
    }

    [Authorize(Roles = nameof(UserRole.Borrower))]
    [HttpPost("api/loans/{loanId:guid}/repayments")]
    public async Task<IActionResult> RequestRepayment(Guid loanId, [FromBody] RepaymentRequest request, CancellationToken cancellationToken)
    {
        if (!TryIdempotencyKey(out var key)) return MissingIdempotencyKey();
        var approval = await platform.RequestRepaymentAsync(CurrentUserId(), loanId, request.Amount, key, cancellationToken);
        return Accepted($"/api/approvals/{approval.Id}", approval);
    }

    [Authorize]
    [HttpGet("api/approvals/pending")]
    public async Task<IActionResult> PendingApprovals(CancellationToken cancellationToken) =>
        Ok(await platform.GetPendingApprovalsAsync(CurrentUserId(), cancellationToken));

    [Authorize]
    [HttpPost("api/approvals/{approvalId:guid}/confirm")]
    public async Task<IActionResult> ConfirmApproval(Guid approvalId, CancellationToken cancellationToken)
    {
        if (!TryIdempotencyKey(out var key)) return MissingIdempotencyKey();
        return Ok(await platform.ConfirmApprovalAsync(CurrentUserId(), approvalId, key, cancellationToken));
    }

    [Authorize]
    [HttpPost("api/approvals/{approvalId:guid}/reject")]
    public async Task<IActionResult> RejectApproval(Guid approvalId, CancellationToken cancellationToken)
    {
        await platform.RejectApprovalAsync(CurrentUserId(), approvalId, cancellationToken);
        return NoContent();
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(value, out var id)) return id;
        throw new UnauthorizedAccessException("The access token does not contain a valid user id.");
    }

    private bool TryIdempotencyKey(out string key)
    {
        key = Request.Headers["Idempotency-Key"].ToString();
        return !string.IsNullOrWhiteSpace(key);
    }

    private IActionResult MissingIdempotencyKey() =>
        Problem(statusCode: StatusCodes.Status400BadRequest, title: "Idempotency-Key header is required.");
}
