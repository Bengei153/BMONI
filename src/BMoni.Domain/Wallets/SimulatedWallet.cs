namespace BMoni.Domain.Wallets;

public sealed class SimulatedWallet
{
    private SimulatedWallet() { }
    private SimulatedWallet(Guid userId) { UserId = userId; }
    public Guid UserId { get; private set; }
    public decimal Balance { get; private set; }
    public int Revision { get; private set; }
    public bool IsSimulated => true;

    public static SimulatedWallet Create(Guid userId) => userId == Guid.Empty
        ? throw new ArgumentException("User is required.", nameof(userId)) : new SimulatedWallet(userId);

    public void Credit(decimal amount)
    {
        ValidateAmount(amount);
        Balance += amount;
        Revision++;
    }

    public void Debit(decimal amount)
    {
        ValidateAmount(amount);
        if (amount > Balance) throw new InvalidOperationException("Simulated wallet balance is too low.");
        Balance -= amount;
        Revision++;
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new ArgumentOutOfRangeException(nameof(amount));
    }
}
