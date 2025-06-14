using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Application.DTOs.KnowledgeBase;
using Application.Interfaces;
using Domain.Exceptions;
using MediatR;

namespace Core.Application.UseCases.KnowledgeBase.UpdateKnowledgeBase;

public record UpdateKnowledgeBaseCommand(
    Guid Id,
    Guid UserId,
    UpdateKnowledgeBaseDto UpdateDto
) : IRequest<KnowledgeBaseItemDto>;

public class UpdateKnowledgeBaseHandler : IRequestHandler<UpdateKnowledgeBaseCommand, KnowledgeBaseItemDto>
{
    private readonly IKnowledgeBaseFileRepository _repository;

    public UpdateKnowledgeBaseHandler(IKnowledgeBaseFileRepository repository)
    {
        _repository = repository;
    }

    public async Task<KnowledgeBaseItemDto> Handle(
        UpdateKnowledgeBaseCommand request,
        CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(request.Id, cancellationToken);
        
        if (item == null)
        {
            throw new EntityNotFoundException($"Knowledge base item with ID {request.Id} not found");
        }

        // Ensure user owns this document
        if (item.UserId != request.UserId)
        {
            throw new UnauthorizedAccessException("You do not have access to this document");
        }

        // Update fields
        if (request.UpdateDto.Title != null)
        {
            item.Title = request.UpdateDto.Title;
        }

        if (request.UpdateDto.Description != null)
        {
            item.Description = request.UpdateDto.Description;
        }

        if (request.UpdateDto.Tags != null)
        {
            item.Tags = request.UpdateDto.Tags.ToArray();
        }

        item.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(item, cancellationToken);

        return new KnowledgeBaseItemDto
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
        };
    }
}