namespace BMoni.Domain.Loans;

public sealed class CreditAssessment
{
    private CreditAssessment() { }

    public CreditAssessment(Guid loanId, Guid userId, int walletSignalsScore, int purposeClarity,
        int amountRealism, int repaymentPlanFit, int finalScore, bool aiAvailable, DateTimeOffset createdAt)
    {
        if (loanId == Guid.Empty || userId == Guid.Empty) throw new ArgumentException("Loan and user are required.");
        if (walletSignalsScore is < 0 or > 100 || purposeClarity is < 0 or > 100 || amountRealism is < 0 or > 100 ||
            repaymentPlanFit is < 0 or > 100 || finalScore is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(finalScore), "Assessment scores must be 0-100.");
        Id = Guid.NewGuid(); LoanId = loanId; UserId = userId; WalletSignalsScore = walletSignalsScore;
        PurposeClarity = purposeClarity; AmountRealism = amountRealism; RepaymentPlanFit = repaymentPlanFit;
        FinalScore = finalScore; AiAvailable = aiAvailable; CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid LoanId { get; private set; }
    public Guid UserId { get; private set; }
    public int WalletSignalsScore { get; private set; }
    public int PurposeClarity { get; private set; }
    public int AmountRealism { get; private set; }
    public int RepaymentPlanFit { get; private set; }
    public int FinalScore { get; private set; }
    public bool AiAvailable { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
