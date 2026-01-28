using Application.Interfaces;
using Application.UseCases.PersonalityTemplate.CreateTemplate;
using Core.Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.PersonalityTemplate;

public class CreateTemplateHandlerTests
{
    private readonly Mock<IPersonalityTemplateRepository> _repositoryMock;
    private readonly CreateTemplateHandler _handler;

    public CreateTemplateHandlerTests()
    {
        _repositoryMock = new Mock<IPersonalityTemplateRepository>();
        _handler = new CreateTemplateHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_CreatesTemplate_WithValidInput()
    {
        // Arrange
        var command = new CreateTemplateCommand(
            Name: "New Template",
            Description: "A test template",
            Prompt: "You are a helpful assistant...",
            Category: "Business",
            DisplayOrder: 1,
            IsActive: true
        );

        PersonalityTemplateEntity? capturedTemplate = null;
        _repositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<PersonalityTemplateEntity>(), It.IsAny<CancellationToken>()))
            .Callback<PersonalityTemplateEntity, CancellationToken>((t, _) => capturedTemplate = t)
            .ReturnsAsync((PersonalityTemplateEntity t, CancellationToken _) => t);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Template", result.Name);
        Assert.Equal("A test template", result.Description);
        Assert.Equal("Business", result.Category);
        Assert.NotNull(capturedTemplate);
        Assert.False(capturedTemplate.IsSystem); // User-created templates are not system templates
    }

    [Fact]
    public async Task Handle_SetsIsSystemToFalse_ForUserCreatedTemplates()
    {
        // Arrange
        var command = new CreateTemplateCommand(
            Name: "User Template",
            Description: "User created",
            Prompt: "Test prompt",
            Category: "Custom",
            DisplayOrder: 5,
            IsActive: true
        );

        PersonalityTemplateEntity? capturedTemplate = null;
        _repositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<PersonalityTemplateEntity>(), It.IsAny<CancellationToken>()))
            .Callback<PersonalityTemplateEntity, CancellationToken>((t, _) => capturedTemplate = t)
            .ReturnsAsync((PersonalityTemplateEntity t, CancellationToken _) => t);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedTemplate);
        Assert.False(capturedTemplate.IsSystem);
    }

    [Fact]
    public async Task Handle_SetsCreatedAndUpdatedTimestamps()
    {
        // Arrange
        var command = new CreateTemplateCommand(
            Name: "Timestamped Template",
            Description: "Test",
            Prompt: "Test prompt",
            Category: "Test",
            DisplayOrder: 1,
            IsActive: true
        );

        var beforeCreate = DateTime.UtcNow;
        PersonalityTemplateEntity? capturedTemplate = null;

        _repositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<PersonalityTemplateEntity>(), It.IsAny<CancellationToken>()))
            .Callback<PersonalityTemplateEntity, CancellationToken>((t, _) => capturedTemplate = t)
            .ReturnsAsync((PersonalityTemplateEntity t, CancellationToken _) => t);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        var afterCreate = DateTime.UtcNow;

        // Assert
        Assert.NotNull(capturedTemplate);
        Assert.True(capturedTemplate.CreatedAt >= beforeCreate);
        Assert.True(capturedTemplate.CreatedAt <= afterCreate);
        Assert.True(capturedTemplate.UpdatedAt >= beforeCreate);
        Assert.True(capturedTemplate.UpdatedAt <= afterCreate);
    }

    [Fact]
    public async Task Handle_GeneratesNewGuid_ForTemplateId()
    {
        // Arrange
        var command = new CreateTemplateCommand(
            Name: "GUID Template",
            Description: "Test",
            Prompt: "Test prompt",
            Category: "Test",
            DisplayOrder: 1,
            IsActive: true
        );

        PersonalityTemplateEntity? capturedTemplate = null;
        _repositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<PersonalityTemplateEntity>(), It.IsAny<CancellationToken>()))
            .Callback<PersonalityTemplateEntity, CancellationToken>((t, _) => capturedTemplate = t)
            .ReturnsAsync((PersonalityTemplateEntity t, CancellationToken _) => t);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedTemplate);
        Assert.NotEqual(Guid.Empty, capturedTemplate.Id);
    }

    [Fact]
    public async Task Handle_ReturnsCreatedTemplateDto()
    {
        // Arrange
        var command = new CreateTemplateCommand(
            Name: "Return Test",
            Description: "Description",
            Prompt: "Prompt",
            Category: "Category",
            DisplayOrder: 3,
            IsActive: false
        );

        _repositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<PersonalityTemplateEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersonalityTemplateEntity t, CancellationToken _) => t);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Return Test", result.Name);
        Assert.Equal("Description", result.Description);
        Assert.Equal("Prompt", result.Prompt);
        Assert.Equal("Category", result.Category);
        Assert.Equal(3, result.DisplayOrder);
        Assert.False(result.IsActive);
    }
}
