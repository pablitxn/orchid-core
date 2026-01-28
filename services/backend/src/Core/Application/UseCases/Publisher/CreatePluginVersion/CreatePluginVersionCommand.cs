using Application.UseCases.Publisher.SubmitPlugin;
using MediatR;

namespace Application.UseCases.Publisher.CreatePluginVersion;

public record CreatePluginVersionCommand(
    Guid UserId,
    Guid PluginId,
    string Version,
    string? ReleaseNotes,
    string? ManifestUrl,
    string? Compatibility
) : IRequest<SubmissionDto>;
