using Application.Interfaces;
using Domain.Enums;
using MediatR;

namespace Application.UseCases.Admin.ListSubmissions;

public class ListSubmissionsHandler(
    IPluginSubmissionRepository submissionRepository
) : IRequestHandler<ListSubmissionsQuery, List<AdminSubmissionDto>>
{
    private readonly IPluginSubmissionRepository _submissionRepository = submissionRepository;

    public async Task<List<AdminSubmissionDto>> Handle(ListSubmissionsQuery request, CancellationToken cancellationToken)
    {
        var submissions = request.Status.HasValue
            ? await _submissionRepository.ListByStatusAsync(request.Status.Value, cancellationToken)
            : await _submissionRepository.ListPendingAsync(cancellationToken);

        return submissions.Select(s => new AdminSubmissionDto
        {
            Id = s.Id,
            PublisherId = s.PublisherId,
            PublisherName = s.Publisher?.Name ?? "Unknown",
            PluginId = s.PluginId,
            Name = s.Name,
            Slug = s.Slug,
            ShortDescription = s.ShortDescription,
            FullDescription = s.FullDescription,
            Category = s.Category,
            Tags = s.Tags,
            Version = s.Version,
            ManifestUrl = s.ManifestUrl,
            RepositoryUrl = s.RepositoryUrl,
            Compatibility = s.Compatibility,
            Status = s.Status,
            ReviewedBy = s.ReviewedBy,
            ReviewNotes = s.ReviewNotes,
            ReviewedAt = s.ReviewedAt,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }).ToList();
    }
}
