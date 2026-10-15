using BMoni.Application.Auth;
using BMoni.Infrastructure.Auth;
using BMoni.Infrastructure.Persistence;
using BMoni.Infrastructure.Bmoni;
using BMoni.Application.Bmoni;
using BMoni.Application.Loans;
using BMoni.Infrastructure.Platform;
using BMoni.Infrastructure.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BMoni.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var connection = configuration["DATABASE_URL"] ?? configuration.GetConnectionString("Default") ?? "Data Source=bmoni.db";
        services.AddDbContext<AppDbContext>(options =>
        {
            if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase) ||
                connection.StartsWith("Host=", StringComparison.OrdinalIgnoreCase) ||
                connection.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
                options.UseNpgsql(ToNpgsqlConnectionString(connection));
            else
                options.UseSqlite(connection);
        });
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.PostConfigure<JwtOptions>(options => options.Secret = configuration["JWT_SECRET"] ?? options.Secret);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPlatformService, PlatformService>();
        services.AddScoped<IBmoniWebhookService, BmoniWebhookService>();
        services.AddHttpClient<IAdvisoryService, AdvisoryService>(client =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHostedService<LoanExpirationWorker>();
        services.AddHostedService<BmoniTransferReconciliationWorker>();
        services.AddHostedService<BmoniWebhookWorker>();
        services.Configure<BmoniOptions>(options =>
        {
            options.BaseUrl = configuration["BMONI_BASE_URL"] ?? configuration[$"{BmoniOptions.SectionName}:BaseUrl"] ?? options.BaseUrl;
            options.ApiKey = configuration["BMONI_API_KEY"] ?? configuration[$"{BmoniOptions.SectionName}:ApiKey"] ?? options.ApiKey;
            if (int.TryParse(configuration[$"{BmoniOptions.SectionName}:RequestTimeoutSeconds"], out var timeoutSeconds)) options.RequestTimeoutSeconds = timeoutSeconds;
            if (int.TryParse(configuration[$"{BmoniOptions.SectionName}:PollIntervalMilliseconds"], out var pollMs)) options.PollIntervalMilliseconds = pollMs;
            if (int.TryParse(configuration[$"{BmoniOptions.SectionName}:PollTimeoutSeconds"], out var pollSeconds)) options.PollTimeoutSeconds = pollSeconds;
        });
        services.Configure<SignerOptions>(options =>
        {
            options.Url = configuration["SIGNER_URL"] ?? configuration[$"{SignerOptions.SectionName}:Url"] ?? options.Url;
            options.Secret = configuration["SIGNER_SECRET"] ?? configuration[$"{SignerOptions.SectionName}:Secret"] ?? options.Secret;
        });
        services.AddHttpClient<IBmoniGateway, BmoniGateway>((provider, client) =>
        {
            var config = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BmoniOptions>>().Value;
            client.BaseAddress = new Uri(config.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds);
        });
        services.AddHttpClient<ISignerClient, SignerClient>((provider, client) =>
        {
            var config = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SignerOptions>>().Value;
            client.BaseAddress = new Uri(config.Url.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        return services;
    }

    private static string ToNpgsqlConnectionString(string connection)
    {
        if (!Uri.TryCreate(connection, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
            return connection;

        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        if (string.IsNullOrWhiteSpace(database))
            throw new InvalidOperationException("DATABASE_URL must include a PostgreSQL database name.");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = database,
            Username = Uri.UnescapeDataString(uri.UserInfo.Split(':', 2)[0]),
            Password = Uri.UnescapeDataString(uri.UserInfo.Contains(':')
                ? uri.UserInfo[(uri.UserInfo.IndexOf(':') + 1)..]
                : string.Empty)
        };

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in query)
        {
            var parts = item.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<SslMode>(value, true, out var sslMode))
                builder.SslMode = sslMode;
        }

        return builder.ConnectionString;
    }
}
