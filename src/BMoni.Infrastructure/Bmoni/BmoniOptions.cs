namespace BMoni.Infrastructure.Bmoni;

public sealed class BmoniOptions
{
    public const string SectionName = "Bmoni";
    public string BaseUrl { get; set; } = "https://embedded-dev.bmoni.com";
    public string ApiKey { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int PollIntervalMilliseconds { get; set; } = 1000;
    public int PollTimeoutSeconds { get; set; } = 90;
}

public sealed class SignerOptions
{
    public const string SectionName = "Signer";
    public string Url { get; set; } = "http://localhost:8090";
    public string Secret { get; set; } = string.Empty;
}
