namespace BMoni.Domain.Users;

[Flags]
public enum UserRole
{
    None = 0,
    Borrower = 1,
    Lender = 2
}
