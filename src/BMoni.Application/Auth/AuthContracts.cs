using BMoni.Domain.Users;
using MediatR;

namespace BMoni.Application.Auth;

public sealed record RegisterRequest(string Email, string FirstName, string Password, UserRole Roles);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthUserDto(Guid Id, string Email, string FirstName, UserRole Roles);
public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, AuthUserDto User);
public sealed class DuplicateEmailException : Exception { }
public sealed record RegisterUserCommand(string Email, string FirstName, string Password, UserRole Roles) : IRequest<AuthResponse>;
public sealed record LoginUserCommand(string Email, string Password) : IRequest<AuthResponse?>;
public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<AuthUserDto?>;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthUserDto?> GetUserAsync(Guid id, CancellationToken cancellationToken);
}

internal sealed class RegisterUserHandler(IAuthService auth) : IRequestHandler<RegisterUserCommand, AuthResponse>
{
    public Task<AuthResponse> Handle(RegisterUserCommand request, CancellationToken cancellationToken) =>
        auth.RegisterAsync(new RegisterRequest(request.Email, request.FirstName, request.Password, request.Roles), cancellationToken);
}

internal sealed class LoginUserHandler(IAuthService auth) : IRequestHandler<LoginUserCommand, AuthResponse?>
{
    public Task<AuthResponse?> Handle(LoginUserCommand request, CancellationToken cancellationToken) =>
        auth.LoginAsync(new LoginRequest(request.Email, request.Password), cancellationToken);
}

internal sealed class GetCurrentUserHandler(IAuthService auth) : IRequestHandler<GetCurrentUserQuery, AuthUserDto?>
{
    public Task<AuthUserDto?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken) =>
        auth.GetUserAsync(request.UserId, cancellationToken);
}
