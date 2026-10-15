namespace BMoni.Domain.Users;

public sealed class User
{
    private User() { }

    private User(Guid id, string email, string firstName, string passwordHash, UserRole roles, DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        FirstName = firstName;
        PasswordHash = passwordHash;
        Roles = roles;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Roles { get; private set; }
    public string? BmoniUserId { get; private set; }
    public string? BmoniOwnerAddress { get; private set; }
    public string? BmoniSmartWalletId { get; private set; }
    public string? BmoniWalletAddress { get; private set; }
    public string? BmoniOnboardingStatus { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static User Register(string email, string firstName, string passwordHash, UserRole roles, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentException("First name is required.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        if (roles == UserRole.None || (roles & ~(UserRole.Borrower | UserRole.Lender)) != 0)
            throw new ArgumentOutOfRangeException(nameof(roles));

        return new User(Guid.NewGuid(), email.Trim().ToLowerInvariant(), firstName.Trim(), passwordHash, roles, now);
    }

    public bool HasRole(UserRole role) => (Roles & role) == role;
    public void LinkBmoniUser(string bmoniUserId)
    {
        if (string.IsNullOrWhiteSpace(bmoniUserId)) throw new ArgumentException("BMONI user id is required.", nameof(bmoniUserId));
        if (BmoniUserId is not null && BmoniUserId != bmoniUserId)
            throw new InvalidOperationException("This account is already linked to a different BMONI user.");
        BmoniUserId = bmoniUserId;
    }

    public void RecordBmoniWallet(string ownerAddress, string smartWalletId, string walletAddress)
    {
        if (string.IsNullOrWhiteSpace(ownerAddress) || string.IsNullOrWhiteSpace(smartWalletId) || string.IsNullOrWhiteSpace(walletAddress))
            throw new ArgumentException("BMONI wallet details are required.");
        BmoniOwnerAddress = ownerAddress;
        BmoniSmartWalletId = smartWalletId;
        BmoniWalletAddress = walletAddress;
    }

    public void RecordBmoniOnboardingStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) throw new ArgumentException("BMONI onboarding status is required.", nameof(status));
        BmoniOnboardingStatus = status;
    }
}
