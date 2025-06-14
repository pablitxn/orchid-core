using Application.Interfaces;
using Application.UseCases.Team.ListTeams;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.Team;

public class ListTeamsHandlerTests
{
    private readonly ListTeamsHandler _handler;
    private readonly Mock<ITeamRepository> _repo = new();

    public ListTeamsHandlerTests()
    {
        _handler = new ListTeamsHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsTeams()
    {
        var expected = new List<TeamEntity> { new() { Id = Guid.NewGuid(), Name = "team" } };
        _repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _handler.Handle(new ListTeamsQuery(), CancellationToken.None);

        Assert.Equal(expected.Count, result.Count);
    }
}