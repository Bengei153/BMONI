namespace BMoni.Domain.Loans;

public sealed record RepaymentAllocation(Guid LenderId, decimal Amount);

public static class ProRataAllocator
{
    public static IReadOnlyList<RepaymentAllocation> Allocate(decimal payment, IReadOnlyList<(Guid LenderId, decimal Share, DateTimeOffset CreatedAt)> shares)
    {
        if (payment <= 0 || decimal.Round(payment, 2) != payment) throw new ArgumentOutOfRangeException(nameof(payment));
        var eligible = shares.Where(share => share.Share > 0).ToArray();
        if (eligible.Length == 0) throw new ArgumentException("At least one lender share is required.", nameof(shares));
        var total = eligible.Sum(share => share.Share);
        if (total <= 0) throw new ArgumentException("Total lender shares must be positive.", nameof(shares));

        var allocations = eligible.Select(share => new RepaymentAllocation(share.LenderId,
            decimal.Floor(payment * share.Share / total * 100m) / 100m)).ToArray();
        var remainder = payment - allocations.Sum(allocation => allocation.Amount);
        var remainderRecipient = eligible.OrderByDescending(share => share.Share).ThenBy(share => share.CreatedAt).First().LenderId;
        var targetIndex = Array.FindIndex(allocations, allocation => allocation.LenderId == remainderRecipient);
        allocations[targetIndex] = allocations[targetIndex] with { Amount = allocations[targetIndex].Amount + remainder };
        return allocations;
    }
}
