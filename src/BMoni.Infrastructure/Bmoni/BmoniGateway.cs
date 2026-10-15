using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using BMoni.Application.Bmoni;
using BMoni.Domain.Bmoni;
using BMoni.Domain.Loans;
using BMoni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BMoni.Infrastructure.Bmoni;

internal sealed class BmoniGateway(HttpClient http, ISignerClient signer, AppDbContext db, IOptions<BmoniOptions> options) : IBmoniGateway
{
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private readonly BmoniOptions _options = options.Value;

    public async Task<BmoniProvisionedAccount> ProvisionNigeriaAccountAsync(Guid localUserId, BmoniUserProfile profile,
        BmoniKycSubmission kyc, CancellationToken cancellationToken)
    {
        ValidateProfile(profile);
        ValidateKyc(kyc);
        var localUser = await db.Users.SingleOrDefaultAsync(user => user.Id == localUserId, cancellationToken)
            ?? throw new KeyNotFoundException("The local user for BMONI onboarding was not found.");
        var bmoniUserId = localUser.BmoniUserId;
        if (bmoniUserId is null)
        {
            using var userResponse = await SendAsync(HttpMethod.Post, "/v1/users", new
            {
                firstName = profile.FirstName,
                lastName = profile.LastName,
                email = profile.Email,
                phoneNumber = profile.PhoneNumber,
                bvn = profile.Bvn
            }, cancellationToken);
            bmoniUserId = FindRequiredString(userResponse.RootElement, "bmoniUserId");
            localUser.LinkBmoniUser(bmoniUserId);
            await db.SaveChangesAsync(cancellationToken);
        }

        var signerUserKey = localUserId.ToString("N");
        var ownerAddress = await signer.InitWalletAsync(signerUserKey, cancellationToken);
        var smartWalletId = localUser.BmoniSmartWalletId;
        var walletAddress = localUser.BmoniWalletAddress;
        if (smartWalletId is null || walletAddress is null)
        {
            using var challengeResponse = await SendAsync(HttpMethod.Post,
                $"/v1/users/{Escape(bmoniUserId)}/smart-wallets/owner-proof-challenges",
                new { currency = "CNGN", userOwnerAddress = ownerAddress }, cancellationToken);
            var challengeId = FindRequiredString(challengeResponse.RootElement, "challengeId");
            var challengeMessage = FindRequiredString(challengeResponse.RootElement, "message");
            var ownerProofSignature = await signer.SignMessageAsync(signerUserKey, challengeMessage, cancellationToken);

            using var walletResponse = await SendAsync(HttpMethod.Post,
                $"/v1/users/{Escape(bmoniUserId)}/smart-wallets/create-managed",
                new { currency = "CNGN", userOwnerAddress = ownerAddress, ownerProofChallengeId = challengeId, ownerProofSignature },
                cancellationToken);
            smartWalletId = FindRequiredString(walletResponse.RootElement, "smartWalletId");
            walletAddress = FindRequiredString(walletResponse.RootElement, "address");
            localUser.RecordBmoniWallet(ownerAddress, smartWalletId, walletAddress);
            await db.SaveChangesAsync(cancellationToken);
        }

        await UploadKycDocumentAsync(bmoniUserId, "/kyc/documents/identification", kyc.IdentificationFiles,
            new Dictionary<string, string>
            {
                ["type"] = kyc.IdentificationType,
                ["documentNumber"] = kyc.IdentificationNumber,
                ["issuingCountry"] = kyc.IdentificationIssuingCountry
            }, cancellationToken);
        await UploadKycDocumentAsync(bmoniUserId, "/kyc/documents/proof-of-address", kyc.ProofOfAddressFiles,
            new Dictionary<string, string> { ["type"] = kyc.ProofOfAddressType }, cancellationToken);

        await SendAsync(HttpMethod.Patch, $"/v1/users/{Escape(bmoniUserId)}/kyc", new
        {
            personalInfo = new
            {
                firstName = profile.FirstName,
                lastName = profile.LastName,
                email = profile.Email,
                phoneNumber = profile.PhoneNumber,
                dateOfBirth = kyc.PersonalInfo.DateOfBirth,
                gender = kyc.PersonalInfo.Gender,
                nationality = kyc.PersonalInfo.Nationality,
                occupation = kyc.PersonalInfo.Occupation
            },
            address = new
            {
                streetLine1 = kyc.Address.StreetLine1,
                city = kyc.Address.City,
                state = kyc.Address.State,
                postalCode = kyc.Address.PostalCode,
                countryCode = kyc.Address.CountryCode
            },
            identificationNumbers = new[]
            {
                new { type = "bvn", number = profile.Bvn, issuingCountryCode = "NGA" }
            }
        }, cancellationToken);

        using var readiness = await SendAsync(HttpMethod.Get,
            $"/v1/users/{Escape(bmoniUserId)}/kyc/readiness", null, cancellationToken);
        if (FindOptionalBoolean(readiness.RootElement, "ready") is not true)
            throw new InvalidOperationException("BMONI KYC is not ready for activation. Complete the required sandbox KYC profile and documents before starting Nigeria onboarding.");
        await SendAsync(HttpMethod.Post, $"/v1/users/{Escape(bmoniUserId)}/kyc/activate", null, cancellationToken);

        await SendAsync(HttpMethod.Post, $"/v1/users/{Escape(bmoniUserId)}/onboarding/start-nigeria",
            new { bvn = profile.Bvn, ngnWalletAddress = ownerAddress, ngnWalletIndex = 0 }, cancellationToken);

        using var statusResponse = await SendAsync(HttpMethod.Get,
            $"/v1/users/{Escape(bmoniUserId)}/onboarding/status", null, cancellationToken);
        var status = FindOptionalString(statusResponse.RootElement, "status") ?? "unknown";
        localUser.RecordBmoniOnboardingStatus(status);
        await db.SaveChangesAsync(cancellationToken);
        return new BmoniProvisionedAccount(bmoniUserId, ownerAddress, smartWalletId, walletAddress, status);
    }

    public async Task<JsonElement> GetBalancesAsync(Guid localUserId, CancellationToken cancellationToken)
    {
        var user = await FindLinkedUserAsync(localUserId, cancellationToken);
        using var response = await SendAsync(HttpMethod.Get, $"/v1/users/{Escape(user.BmoniUserId!)}/smart-wallets/account/balances", null, cancellationToken);
        return response.RootElement.Clone();
    }

    public async Task<JsonElement> GetTransactionsAsync(Guid localUserId, CancellationToken cancellationToken)
    {
        var user = await FindLinkedUserAsync(localUserId, cancellationToken);
        using var response = await SendAsync(HttpMethod.Get,
            $"/v1/users/{Escape(user.BmoniUserId!)}/smart-wallets/{Escape(user.BmoniSmartWalletId!)}/transactions", null, cancellationToken);
        return response.RootElement.Clone();
    }

    public async Task<BmoniAccountVerification> VerifyNigerianAccountAsync(Guid localUserId, string bankCode,
        string accountNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bankCode) || bankCode.Length > 20)
            throw new ArgumentException("A valid Nigerian bank code is required.", nameof(bankCode));
        if (!Regex.IsMatch(accountNumber ?? string.Empty, @"^\d{10}$"))
            throw new ArgumentException("Nigerian account number must contain exactly 10 digits.", nameof(accountNumber));
        var user = await FindLinkedUserAsync(localUserId, cancellationToken);
        try
        {
            using var response = await SendAsync(HttpMethod.Post,
                $"/v1/users/{Escape(user.BmoniUserId!)}/bank-accounts/verify-nigerian-account",
                new { accountNumber, bankCode }, cancellationToken);
            var accountName = FindOptionalString(response.RootElement, "accountHolderName") ??
                              FindOptionalString(response.RootElement, "accountName");
            return new BmoniAccountVerification(!string.IsNullOrWhiteSpace(accountName), accountName);
        }
        catch (BmoniApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return new BmoniAccountVerification(false, null);
        }
    }

    public async Task<BmoniTransferResult> TransferAsync(Guid localUserId, string toBmoniUserId,
        decimal amount, string description, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(description) || description.Length > 500) throw new ArgumentException("Description must be 1-500 characters.", nameof(description));

        if (idempotencyKey.Length > 200) throw new ArgumentException("Idempotency key cannot exceed 200 characters.", nameof(idempotencyKey));
        var user = await FindLinkedUserAsync(localUserId, cancellationToken);
        var fromBmoniUserId = user.BmoniUserId!;
        var smartWalletId = user.BmoniSmartWalletId!;
        var ownerKeyId = localUserId.ToString("N");
        var transfer = await db.BmoniProposals.SingleOrDefaultAsync(record => record.IdempotencyKey == idempotencyKey, cancellationToken);
        var isNewTransfer = transfer is null;
        if (transfer is not null && !transfer.Matches(fromBmoniUserId, ownerKeyId, smartWalletId, toBmoniUserId, amount, description))
            throw new InvalidOperationException("Idempotency key was already used for a different transfer.");

        if (transfer is null)
        {
            transfer = BmoniProposalRecord.Begin(idempotencyKey, fromBmoniUserId, ownerKeyId, smartWalletId,
                toBmoniUserId, amount, description, DateTimeOffset.UtcNow);
            db.BmoniProposals.Add(transfer);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (transfer.Status == "COMPLETED")
            return new BmoniTransferResult(transfer.BmoniProposalId!, transfer.Status, IsSimulated: false);

        var proposalId = transfer.BmoniProposalId;
        if (proposalId is null)
        {
            if (!isNewTransfer)
                throw new InvalidOperationException("A prior transfer attempt did not record its BMONI proposal id. Reconcile this transfer before retrying to prevent a duplicate payment.");
            var proposalResponse = await SendAsync(HttpMethod.Post,
                $"/v1/users/{Escape(fromBmoniUserId)}/smart-wallets/{Escape(smartWalletId)}/proposals",
                new { proposal = new { type = "TRANSFER", toUserId = toBmoniUserId,
                    amount = amount.ToString("0.00", CultureInfo.InvariantCulture), currency = "CNGN", description } }, cancellationToken);
            proposalId = FindRequiredString(proposalResponse.RootElement, "id");
            transfer.RecordProposal(proposalId, FindOptionalString(proposalResponse.RootElement, "status") ?? "PENDING_APPROVALS", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }

        await SendAsync(HttpMethod.Post, $"/v1/users/{Escape(fromBmoniUserId)}/smart-wallets/proposals/{Escape(proposalId)}/approve", null, cancellationToken);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(_options.PollTimeoutSeconds);
        string hash;
        while (true)
        {
            try
            {
                using var payload = await SendAsync(HttpMethod.Get,
                    $"/v1/users/{Escape(fromBmoniUserId)}/smart-wallets/proposals/{Escape(proposalId)}/sign-payload", null, cancellationToken);
                hash = FindRequiredString(payload.RootElement, "hashToSign");
                break;
            }
            catch (BmoniApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(_options.PollIntervalMilliseconds, cancellationToken);
            }
        }
        transfer.RecordStatus("PENDING_SIGNATURES", DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        var signature = await signer.SignHashAsync(ownerKeyId, hash, cancellationToken);
        await SendAsync(HttpMethod.Post,
            $"/v1/users/{Escape(fromBmoniUserId)}/smart-wallets/proposals/{Escape(proposalId)}/sign",
            new { signature }, cancellationToken);

        string status;
        do
        {
            using var proposal = await SendAsync(HttpMethod.Get,
                $"/v1/users/{Escape(fromBmoniUserId)}/smart-wallets/proposals/{Escape(proposalId)}", null, cancellationToken);
            status = FindRequiredString(proposal.RootElement, "status");
            transfer.RecordStatus(status, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            if (status is "COMPLETED" or "FAILED") break;
            if (DateTimeOffset.UtcNow >= deadline) throw new TimeoutException("BMONI proposal did not reach a terminal state before timeout.");
            await Task.Delay(_options.PollIntervalMilliseconds, cancellationToken);
        } while (true);

        return new BmoniTransferResult(proposalId, status, IsSimulated: false);
    }

    public async Task ReconcilePendingTransfersAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey)) return;
        var approvals = await db.Approvals
            .Where(item => item.Type == ApprovalType.BmoniTransfer && !item.Confirmed && !item.Rejected)
            .ToListAsync(cancellationToken);
        var approvalKeys = approvals.Select(item => $"approval:{item.Id:N}").ToArray();
        if (approvalKeys.Length == 0) return;
        var approvalsByKey = approvals.ToDictionary(item => $"approval:{item.Id:N}");
        var pending = await db.BmoniProposals
            .Where(record => record.BmoniProposalId != null && approvalKeys.Contains(record.IdempotencyKey))
            .ToListAsync(cancellationToken);
        foreach (var transfer in pending)
        {
            var status = transfer.Status;
            if (status is not ("COMPLETED" or "FAILED"))
            {
                using var response = await SendAsync(HttpMethod.Get,
                    $"/v1/users/{Escape(transfer.FromBmoniUserId)}/smart-wallets/proposals/{Escape(transfer.BmoniProposalId!)}",
                    null, cancellationToken);
                status = FindRequiredString(response.RootElement, "status");
            }
            transfer.RecordStatus(status, DateTimeOffset.UtcNow);
            if (!approvalsByKey.TryGetValue(transfer.IdempotencyKey, out var approval)) continue;
            if (status == "COMPLETED")
                approval.Confirm($"reconcile:{approval.Id:N}", DateTimeOffset.UtcNow);
            else if (status == "FAILED")
                approval.Reject();
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<BMoni.Domain.Users.User> FindLinkedUserAsync(Guid localUserId, CancellationToken cancellationToken) =>
        await db.Users.SingleOrDefaultAsync(user => user.Id == localUserId && user.BmoniUserId != null &&
            user.BmoniSmartWalletId != null, cancellationToken)
        ?? throw new InvalidOperationException("Complete BMONI sandbox onboarding before using wallet operations.");

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey)) throw new InvalidOperationException("BMONI_API_KEY is not configured.");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("x-api-key", _options.ApiKey);
        if (body is not null) request.Content = JsonContent.Create(body, options: _json);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new BmoniApiException(response.StatusCode, $"{method} {path}");
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(responseBody) ? "{}" : responseBody);
    }

    private async Task UploadKycDocumentAsync(string bmoniUserId, string endpoint, IReadOnlyList<BmoniKycFile> files,
        IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        foreach (var file in files)
        {
            var content = new ByteArrayContent(file.Content);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            form.Add(content, "files", file.FileName);
        }
        foreach (var (name, value) in fields) form.Add(new StringContent(value), name);

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/v1/users/{Escape(bmoniUserId)}{endpoint}") { Content = form };
        if (string.IsNullOrWhiteSpace(_options.ApiKey)) throw new InvalidOperationException("BMONI_API_KEY is not configured.");
        request.Headers.Add("x-api-key", _options.ApiKey);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new BmoniApiException(response.StatusCode, $"POST /v1/users/{Escape(bmoniUserId)}{endpoint}");
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string FindRequiredString(JsonElement root, string propertyName) => FindOptionalString(root, propertyName)
        ?? throw new InvalidDataException($"BMONI response did not contain the documented '{propertyName}' field.");

    private static string? FindOptionalString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString();
            foreach (var property in element.EnumerateObject())
                if (FindOptionalString(property.Value, propertyName) is { } found) return found;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray())
                if (FindOptionalString(item, propertyName) is { } found) return found;
        return null;
    }

    private static bool? FindOptionalBoolean(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return value.GetBoolean();
            foreach (var property in element.EnumerateObject())
                if (FindOptionalBoolean(property.Value, propertyName) is { } found) return found;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray())
                if (FindOptionalBoolean(item, propertyName) is { } found) return found;
        return null;
    }

    private static void ValidateProfile(BmoniUserProfile profile)
    {
        var validSandboxPersona = (profile.FirstName, profile.LastName, profile.PhoneNumber, profile.Bvn) switch
        {
            ("Bunch", "Dillon", "+2348000000000", "95888168924") => true,
            ("Samson", "Jabo", "+2348000000001", "22222222222") => true,
            _ => false
        };
        if (!validSandboxPersona || string.IsNullOrWhiteSpace(profile.Email))
            throw new ArgumentException("Use one of BMONI's exact Nigeria sandbox personas (name, E.164 phone, and BVN).", nameof(profile));
    }

    private static void ValidateKyc(BmoniKycSubmission kyc)
    {
        if (string.IsNullOrWhiteSpace(kyc.IdentificationType) || string.IsNullOrWhiteSpace(kyc.IdentificationNumber) ||
            string.IsNullOrWhiteSpace(kyc.IdentificationIssuingCountry) || kyc.IdentificationFiles.Count is < 1 or > 2 ||
            kyc.ProofOfAddressFiles.Count is < 1 or > 2 || string.IsNullOrWhiteSpace(kyc.ProofOfAddressType) ||
            string.IsNullOrWhiteSpace(kyc.PersonalInfo.DateOfBirth) || string.IsNullOrWhiteSpace(kyc.PersonalInfo.Gender) ||
            string.IsNullOrWhiteSpace(kyc.PersonalInfo.Nationality) || string.IsNullOrWhiteSpace(kyc.PersonalInfo.Occupation) ||
            string.IsNullOrWhiteSpace(kyc.Address.StreetLine1) || string.IsNullOrWhiteSpace(kyc.Address.City) ||
            string.IsNullOrWhiteSpace(kyc.Address.State) || string.IsNullOrWhiteSpace(kyc.Address.PostalCode) ||
            kyc.Address.CountryCode != "NGA" || kyc.Address.PostalCode.Length != 6 ||
            kyc.Address.PostalCode.Any(character => !char.IsAsciiDigit(character)) ||
            !DateOnly.TryParseExact(kyc.PersonalInfo.DateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _))
            throw new ArgumentException("Sandbox KYC requires ID and proof-of-address files plus complete profile and address details.", nameof(kyc));
        if (kyc.IdentificationFiles.Concat(kyc.ProofOfAddressFiles).Any(file =>
                file.Content.Length == 0 || file.Content.Length > 10 * 1024 * 1024 ||
                string.IsNullOrWhiteSpace(file.FileName) || string.IsNullOrWhiteSpace(file.ContentType)))
            throw new ArgumentException("KYC document files must include a file name, content type, and content no larger than 10 MB.", nameof(kyc));
    }
}
