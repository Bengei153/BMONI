using BMoni.Domain.Loans;

namespace BMoni.Domain.Tests;

public sealed class LoanTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(85, 0.03, 20_000)]
    [InlineData(70, 0.05, 10_000)]
    [InlineData(50, 0.08, 5_000)]
    public void Request_UsesConfiguredScoreTier(int score, decimal rate, decimal maximum)
    {
        var loan = Loan.Request(Guid.NewGuid(), maximum, "Books", "Need funds for course materials.", 30, 2, score, Now);

        Assert.Equal(rate, loan.InterestRate);
        Assert.Equal(maximum * (1 + rate), loan.TotalRepayable);
        Assert.Equal(LoanStatus.OpenForFunding, loan.Status);
    }

    [Fact]
    public void Request_BelowMinimumScoreIsDeclined()
    {
        var loan = Loan.Request(Guid.NewGuid(), 1_000m, "Books", "Need funds for course materials.", 30, 1, 49, Now);

        Assert.Equal(LoanStatus.Declined, loan.Status);
        Assert.Equal(0m, loan.InterestRate);
    }

    [Fact]
    public void Funding_CannotOverfundAndFullFundingRequiresAcceptance()
    {
        var loan = Loan.Request(Guid.NewGuid(), 1_000m, "Books", "Need funds for course materials.", 30, 1, 65, Now);
        loan.AddFunding(400m, Now);
        Assert.Throws<ArgumentOutOfRangeException>(() => loan.AddFunding(601m, Now));
        Assert.Equal(400m, loan.FundedAmount);
        loan.AddFunding(600m, Now);
        Assert.Equal(LoanStatus.AwaitingBorrowerAcceptance, loan.Status);
        loan.AcceptTerms(Now);
        Assert.Equal(LoanStatus.Active, loan.Status);
    }

    [Fact]
    public void Funding_CannotBeWithdrawnAfterFullFunding()
    {
        var loan = Loan.Request(Guid.NewGuid(), 1_000m, "Books", "Need funds for course materials.", 30, 1, 65, Now);
        loan.AddFunding(1_000m, Now);

        Assert.Throws<InvalidOperationException>(() => loan.RemoveFunding(100m));
    }

    [Fact]
    public void Repayment_AllowsPartialAmountsThenClosesAtTotal()
    {
        var loan = Loan.Request(Guid.NewGuid(), 1_000m, "Books", "Need funds for course materials.", 30, 2, 65, Now);
        loan.AddFunding(1_000m, Now);
        loan.AcceptTerms(Now);
        loan.ApplyRepayment(300m);

        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.Equal(750m, loan.Outstanding);
        loan.ApplyRepayment(750m);
        Assert.Equal(LoanStatus.Repaid, loan.Status);
        Assert.Equal(0m, loan.Outstanding);
    }

    [Fact]
    public void Contribution_RequiresAcknowledgedTerms()
    {
        Assert.Throws<InvalidOperationException>(() => LoanContribution.Reserve(Guid.NewGuid(), Guid.NewGuid(),
            10m, false, "idem-1", Now));
    }

    [Fact]
    public void Installments_PreserveTotalAndApplyPartialPaymentsInOrder()
    {
        var installments = LoanInstallment.CreateSchedule(Guid.NewGuid(), 100.01m, 3, Now, 30);

        Assert.Equal(100.01m, installments.Sum(item => item.Amount));
        Assert.Equal(new[] { 10, 20, 30 }, installments.Select(item => (int)(item.DueAt - Now).TotalDays));
        Assert.Equal(33.33m, installments[0].ApplyPayment(40m));
        Assert.Equal(6.67m, installments[1].ApplyPayment(6.67m));
        Assert.Equal(InstallmentStatus.Paid, installments[0].Status);
        Assert.Equal(26.66m, installments[1].Outstanding);
    }

    [Fact]
    public void Installment_BecomesLateAfterGraceAndDefaultsAfterFourteenDays()
    {
        var installment = LoanInstallment.CreateSchedule(Guid.NewGuid(), 100m, 1, Now, 30).Single();

        installment.RefreshStatus(installment.DueAt.AddDays(4));
        Assert.Equal(InstallmentStatus.Late, installment.Status);
        installment.RefreshStatus(installment.DueAt.AddDays(15));
        Assert.Equal(InstallmentStatus.Defaulted, installment.Status);
    }

    [Fact]
    public void ProRataAllocation_PreservesEveryCentAndBreaksTiesByTime()
    {
        var olderLender = Guid.NewGuid();
        var newerLender = Guid.NewGuid();
        var thirdLender = Guid.NewGuid();
        var shares = new[]
        {
            (olderLender, 1m, Now),
            (newerLender, 1m, Now.AddMinutes(1)),
            (thirdLender, 1m, Now.AddMinutes(2))
        };

        var result = ProRataAllocator.Allocate(0.05m, shares);

        Assert.Equal(0.05m, result.Sum(item => item.Amount));
        Assert.Equal(0.03m, result.Single(item => item.LenderId == olderLender).Amount);
        Assert.Equal(0.01m, result.Single(item => item.LenderId == newerLender).Amount);
        Assert.Equal(0.01m, result.Single(item => item.LenderId == thirdLender).Amount);
    }
}
