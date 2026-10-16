using System.Text;
using BMoni.Application;
using BMoni.Application.Loans;
using BMoni.Infrastructure;
using BMoni.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var renderPort))
    builder.WebHost.UseUrls($"http://0.0.0.0:{renderPort}");
builder.Logging.AddJsonConsole();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtSecret = builder.Configuration["JWT_SECRET"] ?? builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException("Set JWT_SECRET to at least 32 bytes in User Secrets or the service environment.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "bmoni-api",
        ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"] ?? "bmoni-client",
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options => options.AddPolicy("auth", context =>
    RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
        new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
builder.Services.AddRateLimiter(options => options.AddPolicy("advisory", context =>
    RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
        new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));

var origins = (builder.Configuration["ALLOWED_ORIGINS"] ?? "")
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
if (origins.Length > 0)
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "LMoni API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    { Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    { [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>() });
});

var app = builder.Build();
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers.TryGetValue("X-Correlation-ID", out var supplied) && !string.IsNullOrWhiteSpace(supplied)
        ? supplied.ToString() : context.TraceIdentifier;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    using (app.Logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId })) await next();
});
app.UseExceptionHandler();
app.UseRateLimiter();
if (origins.Length > 0) app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IPlatformService>().BootstrapAsync(CancellationToken.None);
}

app.Run();

public partial class Program { }

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ValidationException validation)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            var details = new Microsoft.AspNetCore.Mvc.ProblemDetails
            { Status = 400, Title = "Request validation failed.", Type = "https://www.rfc-editor.org/rfc/rfc7807" };
            details.Extensions["errors"] = validation.Errors.GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
            details.Extensions["correlationId"] = httpContext.Response.Headers["X-Correlation-ID"].ToString();
            await problemDetailsService.WriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = details });
            return true;
        }

        var (status, title) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "The request contains invalid values."),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "The requested resource was not found."),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "You are not allowed to perform this operation."),
            InvalidOperationException => (StatusCodes.Status409Conflict, exception.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The record changed during this request. Reload it and retry."),
            _ => (StatusCodes.Status500InternalServerError, "The request could not be completed.")
        };
        if (status != StatusCodes.Status500InternalServerError)
        {
            httpContext.Response.StatusCode = status;
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = status, Title = title, Type = "https://www.rfc-editor.org/rfc/rfc7807",
                    Extensions = { ["correlationId"] = httpContext.Response.Headers["X-Correlation-ID"].ToString() }
                }
            });
            return true;
        }

        logger.LogError(exception, "Unhandled API error");
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = 500, Title = "The request could not be completed.", Type = "https://www.rfc-editor.org/rfc/rfc7807",
                Extensions = { ["correlationId"] = httpContext.Response.Headers["X-Correlation-ID"].ToString() }
            }
        });
        return true;
    }
}
