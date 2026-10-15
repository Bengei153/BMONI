namespace BMoni.Application.Loans;

public sealed record ScamCheckRequest(string? BankCode, string? AccountNumber, string? ExpectedAccountName, decimal? Amount, string? Message);
public sealed record ScamReason(string Type, string Text);
public sealed record ScamCheckResponse(string RiskLevel, IReadOnlyList<ScamReason> Reasons, string RecommendedAction,
    string Disclaimer, string? VerifiedAccountName, bool IsAiGenerated, bool IsSimulated);
public sealed record AssistantChatRequest(string Question);
public sealed record AssistantChatResponse(string Answer, bool IsAiGenerated, bool IsSimulated);
public sealed record StatementAssessmentRequest(string Purpose, string Statement, decimal Amount, int RepaymentDays,
    int Installments, decimal AverageDailyDeposits);
public sealed record StatementAssessmentResult(int PurposeClarity, int AmountRealism, int RepaymentPlanFit,
    IReadOnlyList<string> Reasons, IReadOnlyList<string> Concerns, bool AiAvailable);

public interface IAdvisoryService
{
    Task<ScamCheckResponse> CheckScamAsync(Guid userId, ScamCheckRequest request, CancellationToken cancellationToken);
    Task<AssistantChatResponse> ChatAsync(DashboardDto facts, string question, CancellationToken cancellationToken);
    Task<StatementAssessmentResult> AssessStatementAsync(StatementAssessmentRequest request, CancellationToken cancellationToken);
}
