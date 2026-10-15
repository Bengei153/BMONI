namespace BMoni.Domain.Loans;

public sealed class RepaymentShare
{
    private RepaymentShare() { }
    public RepaymentShare(Guid repaymentId, Guid lenderId, decimal amount)
    { Id = Guid.NewGuid(); RepaymentId = repaymentId; LenderId = lenderId; Amount = amount; }
    public Guid Id { get; private set; }
    public Guid RepaymentId { get; private set; }
    public Guid LenderId { get; private set; }
    public decimal Amount { get; private set; }
}
