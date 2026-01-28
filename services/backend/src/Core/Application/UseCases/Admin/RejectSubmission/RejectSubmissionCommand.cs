using MediatR;

namespace Application.UseCases.Admin.RejectSubmission;

public record RejectSubmissionCommand(
    Guid SubmissionId,
    Guid ReviewedBy,
    string Reason
) : IRequest<RejectSubmissionResult>;

public sealed record RejectSubmissionResult
{
    public Guid SubmissionId { get; init; }
    public string PluginName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}
