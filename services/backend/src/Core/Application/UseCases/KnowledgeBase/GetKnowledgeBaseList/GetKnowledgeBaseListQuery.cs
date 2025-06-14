using System.Threading;
using System.Threading.Tasks;
using Core.Application.DTOs.KnowledgeBase;
using Application.Interfaces;
using MediatR;
using System.Linq;

namespace Core.Application.UseCases.KnowledgeBase.GetKnowledgeBaseList;

public record GetKnowledgeBaseListQuery(
    KnowledgeBaseQueryDto Query,
    Guid UserId
) : IRequest<KnowledgeBaseListResponseDto>;

public class GetKnowledgeBaseListHandler : IRequestHandler<GetKnowledgeBaseListQuery, KnowledgeBaseListResponseDto>
{
    private readonly IKnowledgeBaseFileRepository _repository;

    public GetKnowledgeBaseListHandler(IKnowledgeBaseFileRepository repository)
    {
        _repository = repository;
    }

    public async Task<KnowledgeBaseListResponseDto> Handle(
        GetKnowledgeBaseListQuery request,
        CancellationToken cancellationToken)
    {
        var query = request.Query;
        
        // Get paginated results
        var (items, totalCount) = await _repository.GetPaginatedAsync(
            userId: request.UserId,
            searchTerm: query.SearchTerm,
            tags: query.Tags,
            mimeTypes: query.MimeTypes,
            createdAfter: query.CreatedAfter,
            createdBefore: query.CreatedBefore,
            page: query.Page,
            pageSize: query.PageSize,
            sortBy: query.SortBy,
            sortDescending: query.SortDescending,
            cancellationToken: cancellationToken);

        var dtoItems = items.Select(item => new KnowledgeBaseItemDto
        {
            Id = item.Id,
            UserId = item.UserId,
            Title = item.Title,
            Description = item.Description,
            Tags = item.Tags.ToList(),
            MimeType = item.MimeType,
            FileUrl = item.FileUrl,
            FileSize = item.FileSize,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        }).ToList();

        return new KnowledgeBaseListResponseDto
        {
            Items = dtoItems,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
}