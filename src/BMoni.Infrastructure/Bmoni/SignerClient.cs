using System.Net.Http.Json;
using System.Text.Json;
using BMoni.Application.Bmoni;
using Microsoft.Extensions.Options;

namespace BMoni.Infrastructure.Bmoni;

internal sealed class SignerClient(HttpClient http, IOptions<SignerOptions> options) : ISignerClient
{
    private readonly SignerOptions _options = options.Value;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public Task<string> InitWalletAsync(string userKey, CancellationToken cancellationToken) =>
        SendAsync("/wallets/init", new { userKey }, "ownerAddress", cancellationToken);

    public Task<string> SignMessageAsync(string userKey, string message, CancellationToken cancellationToken) =>
        SendAsync("/sign-message", new { userKey, message }, "signature", cancellationToken);

    public Task<string> SignHashAsync(string userKey, string hash, CancellationToken cancellationToken) =>
        SendAsync("/sign-hash", new { userKey, hash }, "signature", cancellationToken);

    private async Task<string> SendAsync(string path, object body, string resultField, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Secret)) throw new InvalidOperationException("SIGNER_SECRET is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: _json) };
        request.Headers.Add("x-signer-secret", _options.Secret);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Signer service returned HTTP {(int)response.StatusCode}.");
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty(resultField, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Signer service response is missing '{resultField}'.");
        return value.GetString()!;
    }
}
