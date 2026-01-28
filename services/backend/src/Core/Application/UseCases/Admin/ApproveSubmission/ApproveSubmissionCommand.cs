using MediatR;

namespace Application.UseCases.Admin.ApproveSubmission;

public record ApproveSubmissionCommand(
    Guid SubmissionId,
    Guid ReviewedBy,
    string? ReviewNotes
) : IRequest<ApproveSubmissionResult>;

public sealed record ApproveSubmissionResult
{
    public Guid SubmissionId { get; init; }
    public Guid PluginId { get; init; }
    public string PluginName { get; init; } = string.Empty;
    public bool IsNewPlugin { get; init; }
}
