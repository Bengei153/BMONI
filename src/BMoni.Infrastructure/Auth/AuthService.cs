using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BMoni.Application.Auth;
using BMoni.Domain.Users;
using BMoni.Infrastructure.Persistence;
using BMoni.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BMoni.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "bmoni-api";
    public string Audience { get; set; } = "bmoni-client";
    public int ExpiryMinutes { get; set; } = 30;
}

internal sealed class AuthService(AppDbContext db, IOptions<JwtOptions> jwtOptions) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            throw new DuplicateEmailException();

        var user = User.Register(email, request.FirstName, BCrypt.Net.BCrypt.HashPassword(request.Password), request.Roles, DateTimeOffset.UtcNow);
        db.Users.Add(user);
        db.SimulatedWallets.Add(SimulatedWallet.Create(user.Id));
        await db.SaveChangesAsync(cancellationToken);
        return CreateResponse(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) return null;
        return CreateResponse(user);
    }

    public async Task<AuthUserDto?> GetUserAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        return user is null ? null : ToDto(user);
    }

    private AuthResponse CreateResponse(User user)
    {
        var options = jwtOptions.Value;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(options.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.FirstName)
        };
        if (user.HasRole(UserRole.Borrower)) claims.Add(new Claim(ClaimTypes.Role, nameof(UserRole.Borrower)));
        if (user.HasRole(UserRole.Lender)) claims.Add(new Claim(ClaimTypes.Role, nameof(UserRole.Lender)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret));
        var token = new JwtSecurityToken(options.Issuer, options.Audience, claims, expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt, ToDto(user));
    }

    private static AuthUserDto ToDto(User user) => new(user.Id, user.Email, user.FirstName, user.Roles);
}

