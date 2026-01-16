using Application.UseCases.Team.CreateTeam;
using FluentValidation;
using WebApi.Models;

namespace WebApi.Validation;

public sealed class CreateTeamRequestValidator : AbstractValidator<CreateTeamRequest>
{
    public CreateTeamRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => x.Description is not null);

        RuleForEach(x => x.Agents)
            .SetValidator(new TeamAgentInputValidator())
            .When(x => x.Agents is not null);
    }
}

public sealed class TeamAgentInputValidator : AbstractValidator<TeamAgentInput>
{
    public TeamAgentInputValidator()
    {
        RuleFor(x => x.AgentId)
            .NotEmpty();

        RuleFor(x => x.Role)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0);
    }
}
