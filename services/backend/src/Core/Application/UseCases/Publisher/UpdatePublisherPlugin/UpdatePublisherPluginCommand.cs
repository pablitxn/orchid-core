using Application.UseCases.Publisher.SubmitPlugin;
using MediatR;

namespace Application.UseCases.Publisher.UpdatePublisherPlugin;

public record UpdatePublisherPluginCommand(
    Guid UserId,
    Guid PluginId,
    string? Name,
    string? ShortDescription,
    string? FullDescription,
    string? Category,
    string? Tags,
    string? ManifestUrl,
    string? RepositoryUrl,
    string? Compatibility
) : IRequest<SubmissionDto>;
