using Application.Interfaces;
using Application.UseCases.PersonalityTemplate.GetTemplate;
using Core.Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.PersonalityTemplate;

public class GetTemplateHandlerTests
{
    private readonly Mock<IPersonalityTemplateRepository> _repositoryMock;
    private readonly GetTemplateHandler _handler;

    public GetTemplateHandlerTests()
    {
        _repositoryMock = new Mock<IPersonalityTemplateRepository>();
        _handler = new GetTemplateHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsTemplateDto_WhenTemplateExists()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var query = new GetTemplateQuery(templateId);

        var template = new PersonalityTemplateEntity
        {
            Id = templateId,
            Name = "Professional Assistant",
            Description = "A professional and formal AI assistant",
            Prompt = "You are a professional assistant...",
            Category = "Business",
            IsSystem = true,
            DisplayOrder = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow
        };

        _repositoryMock
            .Setup(x => x.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(templateId, result.Id);
        Assert.Equal("Professional Assistant", result.Name);
        Assert.Equal("Business", result.Category);
        Assert.True(result.IsSystem);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTemplateNotFound()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var query = new GetTemplateQuery(templateId);

        _repositoryMock
            .Setup(x => x.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersonalityTemplateEntity?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_CallsRepository_WithCorrectId()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var query = new GetTemplateQuery(templateId);

        _repositoryMock
            .Setup(x => x.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersonalityTemplateEntity?)null);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(x => x.GetByIdAsync(templateId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
