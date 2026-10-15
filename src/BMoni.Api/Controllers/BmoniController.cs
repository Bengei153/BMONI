using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BMoni.Application.Bmoni;
using BMoni.Application.Loans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BMoni.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/bmoni")]
public sealed class BmoniController(IBmoniGateway bmoni, IPlatformService platform) : ControllerBase
{
    [HttpPost("onboarding/nigeria")]
    [RequestSizeLimit(22 * 1024 * 1024)]
    [ProducesResponseType<BmoniProvisionedAccount>(StatusCodes.Status200OK)]
    public async Task<IActionResult> OnboardNigeria([FromForm] NigeriaOnboardingRequest request, CancellationToken cancellationToken)
    {
        var identification = await ReadFileAsync(request.IdentificationFile, cancellationToken);
        var proofOfAddress = await ReadFileAsync(request.ProofOfAddressFile, cancellationToken);
        var profile = new BmoniUserProfile(request.FirstName, request.LastName, request.Email, request.PhoneNumber, request.Bvn);
        var kyc = new BmoniKycSubmission(
            request.IdentificationType, request.IdentificationNumber, request.IdentificationIssuingCountry,
            [identification], request.ProofOfAddressType, [proofOfAddress],
            new BmoniKycPersonalInfo(request.DateOfBirth, request.Gender, request.Nationality, request.Occupation),
            new BmoniKycAddress(request.StreetLine1, request.City, request.State, request.PostalCode, "NGA"));

        var account = await bmoni.ProvisionNigeriaAccountAsync(CurrentUserId(), profile, kyc, cancellationToken);
        return Ok(account);
    }

    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances(CancellationToken cancellationToken) =>
        Ok(await bmoni.GetBalancesAsync(CurrentUserId(), cancellationToken));

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(CancellationToken cancellationToken) =>
        Ok(await bmoni.GetTransactionsAsync(CurrentUserId(), cancellationToken));

    [HttpPost("accounts/verify-nigerian")]
    public async Task<IActionResult> VerifyNigerianAccount([FromBody] VerifyNigerianAccountRequest request,
        CancellationToken cancellationToken) =>
        Ok(await bmoni.VerifyNigerianAccountAsync(CurrentUserId(), request.BankCode, request.AccountNumber, cancellationToken));

    [HttpPost("transfers")]
    [ProducesResponseType<ApprovalDto>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Transfer([FromBody] BmoniTransferRequest request, CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Idempotency-Key header is required.");

        var approval = await platform.RequestBmoniTransferAsync(CurrentUserId(), request.ToBmoniUserId, request.Amount,
            request.Description, key.ToString(), cancellationToken);
        return Accepted($"/api/approvals/{approval.Id}", approval);
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(value, out var id)) return id;
        throw new UnauthorizedAccessException("The access token does not contain a valid user id.");
    }

    private static async Task<BmoniKycFile> ReadFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > 10 * 1024 * 1024)
            throw new ArgumentException("Each sandbox KYC file must be between 1 byte and 10 MB.");
        if (file.ContentType is not ("image/jpeg" or "image/png" or "application/pdf"))
            throw new ArgumentException("KYC files must be JPEG, PNG, or PDF.");
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return new BmoniKycFile(Path.GetFileName(file.FileName), file.ContentType, stream.ToArray());
    }
}

public sealed class NigeriaOnboardingRequest
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string PhoneNumber { get; set; }
    public required string Bvn { get; set; }
    public required string IdentificationType { get; set; }
    public required string IdentificationNumber { get; set; }
    public required string IdentificationIssuingCountry { get; set; }
    public required IFormFile IdentificationFile { get; set; }
    public required string ProofOfAddressType { get; set; }
    public required IFormFile ProofOfAddressFile { get; set; }
    public required string DateOfBirth { get; set; }
    public required string Gender { get; set; }
    public required string Nationality { get; set; }
    public required string Occupation { get; set; }
    public required string StreetLine1 { get; set; }
    public required string City { get; set; }
    public required string State { get; set; }
    public required string PostalCode { get; set; }
}

public sealed record BmoniTransferRequest(string ToBmoniUserId, decimal Amount, string Description);
public sealed record VerifyNigerianAccountRequest(string BankCode, string AccountNumber);
