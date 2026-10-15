namespace BMoni.Application.Bmoni;

public sealed record BmoniUserProfile(string FirstName, string LastName, string Email, string PhoneNumber, string Bvn);
public sealed record BmoniKycFile(string FileName, string ContentType, byte[] Content);
public sealed record BmoniKycPersonalInfo(string DateOfBirth, string Gender, string Nationality, string Occupation);
public sealed record BmoniKycAddress(string StreetLine1, string City, string State, string PostalCode, string CountryCode);
public sealed record BmoniKycSubmission(
    string IdentificationType,
    string IdentificationNumber,
    string IdentificationIssuingCountry,
    IReadOnlyList<BmoniKycFile> IdentificationFiles,
    string ProofOfAddressType,
    IReadOnlyList<BmoniKycFile> ProofOfAddressFiles,
    BmoniKycPersonalInfo PersonalInfo,
    BmoniKycAddress Address);
public sealed record BmoniProvisionedAccount(string BmoniUserId, string OwnerAddress, string SmartWalletId, string WalletAddress, string Status);
public sealed record BmoniTransferResult(string ProposalId, string Status, bool IsSimulated);
public sealed record BmoniAccountVerification(bool Verified, string? AccountName);
public sealed record BmoniWebhookReceiveResult(bool Accepted, bool Duplicate, string? Error = null);

public interface ISignerClient
{
    Task<string> InitWalletAsync(string userKey, CancellationToken cancellationToken);
    Task<string> SignMessageAsync(string userKey, string message, CancellationToken cancellationToken);
    Task<string> SignHashAsync(string userKey, string hash, CancellationToken cancellationToken);
}

public interface IBmoniGateway
{
    Task<BmoniProvisionedAccount> ProvisionNigeriaAccountAsync(Guid localUserId, BmoniUserProfile profile,
        BmoniKycSubmission kyc, CancellationToken cancellationToken);
    Task<System.Text.Json.JsonElement> GetBalancesAsync(Guid localUserId, CancellationToken cancellationToken);
    Task<System.Text.Json.JsonElement> GetTransactionsAsync(Guid localUserId, CancellationToken cancellationToken);
    Task<BmoniAccountVerification> VerifyNigerianAccountAsync(Guid localUserId, string bankCode, string accountNumber,
        CancellationToken cancellationToken);
    Task<BmoniTransferResult> TransferAsync(Guid localUserId, string toBmoniUserId,
        decimal amount, string description, string idempotencyKey, CancellationToken cancellationToken);
    Task ReconcilePendingTransfersAsync(CancellationToken cancellationToken);
}

public interface IBmoniWebhookService
{
    Task<BmoniWebhookReceiveResult> ReceiveAsync(byte[] rawBody, string? signature, string? webhookId,
        CancellationToken cancellationToken);
}
