using Application.UseCases.ChatSession.CreateChatSession;
using Application.UseCases.ChatSession.DeleteChatSessions;
using FluentValidation;
using WebApi.Controllers;

namespace WebApi.Validation;

public sealed class CreateChatSessionCommandValidator : AbstractValidator<CreateChatSessionCommand>
{
    public CreateChatSessionCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.AgentId)
            .NotEmpty();

        RuleFor(x => x.SessionId)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Title)
            .MaximumLength(200)
            .When(x => x.Title is not null);
    }
}

public sealed class DeleteChatSessionsCommandValidator : AbstractValidator<DeleteChatSessionsCommand>
{
    public DeleteChatSessionsCommandValidator()
    {
        RuleFor(x => x.Ids)
            .NotNull()
            .Must(ids => ids != null && ids.Any())
            .WithMessage("At least one session ID is required.");

        RuleForEach(x => x.Ids)
            .NotEmpty();
    }
}

public sealed class ArchiveChatSessionRequestValidator : AbstractValidator<ArchiveChatSessionRequest>
{
    public ArchiveChatSessionRequestValidator()
    {
    }
}
