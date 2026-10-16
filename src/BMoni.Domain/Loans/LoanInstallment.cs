namespace BMoni.Domain.Loans;

public enum InstallmentStatus { Pending, Late, Paid, Defaulted }

public sealed class LoanInstallment
{
    private LoanInstallment() { }

    private LoanInstallment(Guid id, Guid loanId, int number, DateTimeOffset dueAt, decimal amount)
    {
        Id = id;
        LoanId = loanId;
        Number = number;
        DueAt = dueAt;
        Amount = amount;
    }

    public Guid Id { get; private set; }
    public Guid LoanId { get; private set; }
    public int Number { get; private set; }
    public DateTimeOffset DueAt { get; private set; }
    public decimal Amount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public InstallmentStatus Status { get; private set; }
    public decimal Outstanding => Amount - PaidAmount;

    public static IReadOnlyList<LoanInstallment> CreateSchedule(Guid loanId, decimal total, int count,
        DateTimeOffset acceptedAt, int repaymentDays)
    {
        if (loanId == Guid.Empty || total <= 0 || decimal.Round(total, 2) != total || count is < 1 or > 4 || repaymentDays < count)
            throw new ArgumentException("Installment schedule inputs are invalid.");

        var regularAmount = decimal.Floor(total / count * 100m) / 100m;
        var installments = new List<LoanInstallment>(count);
        var assigned = 0m;
        for (var number = 1; number <= count; number++)
        {
            var amount = number == count ? total - assigned : regularAmount;
            assigned += amount;
            var dueDays = (int)Math.Ceiling((decimal)repaymentDays * number / count);
            installments.Add(new LoanInstallment(Guid.NewGuid(), loanId, number, acceptedAt.AddDays(dueDays), amount));
        }
        return installments;
    }

    public decimal ApplyPayment(decimal amount)
    {
        if (Status == InstallmentStatus.Paid || amount <= 0 ||
            decimal.Round(amount, 2) != amount)
            throw new InvalidOperationException("Payment cannot be applied to this installment.");
        var applied = Math.Min(amount, Outstanding);
        if (applied <= 0) throw new InvalidOperationException("Installment is already fully paid.");
        PaidAmount += applied;
        if (PaidAmount == Amount) Status = InstallmentStatus.Paid;
        return applied;
    }

    public void RefreshStatus(DateTimeOffset now, int graceDays = 3, int defaultDays = 14)
    {
        if (Status is InstallmentStatus.Paid or InstallmentStatus.Defaulted) return;
        if (now > DueAt.AddDays(defaultDays)) Status = InstallmentStatus.Defaulted;
        else if (now > DueAt.AddDays(graceDays)) Status = InstallmentStatus.Late;
    }
}
