using Application.Interfaces;
using Application.UseCases.PersonalityTemplate.ListTemplates;
using Core.Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.PersonalityTemplate;

public class ListTemplatesHandlerTests
{
    private readonly Mock<IPersonalityTemplateRepository> _repositoryMock;
    private readonly ListTemplatesHandler _handler;

    public ListTemplatesHandlerTests()
    {
        _repositoryMock = new Mock<IPersonalityTemplateRepository>();
        _handler = new ListTemplatesHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsAllTemplates_WhenIncludeInactiveIsFalse()
    {
        // Arrange
        var query = new ListTemplatesQuery(IncludeInactive: false);

        var templates = new List<PersonalityTemplateEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Professional",
                Description = "Professional tone",
                Prompt = "Be professional...",
                Category = "Business",
                IsActive = true,
                DisplayOrder = 1
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Casual",
                Description = "Casual tone",
                Prompt = "Be casual...",
                Category = "Social",
                IsActive = true,
                DisplayOrder = 2
            }
        };

        _repositoryMock
            .Setup(x => x.ListAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Handle_IncludesInactiveTemplates_WhenIncludeInactiveIsTrue()
    {
        // Arrange
        var query = new ListTemplatesQuery(IncludeInactive: true);

        var templates = new List<PersonalityTemplateEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Active Template",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Inactive Template",
                IsActive = false
            }
        };

        _repositoryMock
            .Setup(x => x.ListAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        _repositoryMock.Verify(x => x.ListAsync(true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoTemplatesExist()
    {
        // Arrange
        var query = new ListTemplatesQuery();

        _repositoryMock
            .Setup(x => x.ListAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PersonalityTemplateEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_MapsTemplateProperties_Correctly()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var query = new ListTemplatesQuery();

        var templates = new List<PersonalityTemplateEntity>
        {
            new()
            {
                Id = templateId,
                Name = "Test Template",
                Description = "Test Description",
                Prompt = "Test Prompt",
                Category = "Test Category",
                IsSystem = true,
                DisplayOrder = 5,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow
            }
        };

        _repositoryMock
            .Setup(x => x.ListAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var template = result.First();
        Assert.Equal(templateId, template.Id);
        Assert.Equal("Test Template", template.Name);
        Assert.Equal("Test Description", template.Description);
        Assert.Equal("Test Category", template.Category);
        Assert.True(template.IsSystem);
    }
}
