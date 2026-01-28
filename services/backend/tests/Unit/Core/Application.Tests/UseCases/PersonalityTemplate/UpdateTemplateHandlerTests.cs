using Application.Interfaces;
using Application.UseCases.PersonalityTemplate.UpdateTemplate;
using Core.Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.PersonalityTemplate;

public class UpdateTemplateHandlerTests
{
    private readonly Mock<IPersonalityTemplateRepository> _repositoryMock;
    private readonly UpdateTemplateHandler _handler;

    public UpdateTemplateHandlerTests()
    {
        _repositoryMock = new Mock<IPersonalityTemplateRepository>();
        _handler = new UpdateTemplateHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_UpdatesTemplate_WhenTemplateExists()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var command = new UpdateTemplateCommand(
            Id: templateId,
            Name: "Updated Name",
            Description: "Updated Description",
            Prompt: "Updated Prompt",
            Category: "Updated Category",
            DisplayOrder: 5,
            IsActive: false
        );

        var existingTemplate = new PersonalityTemplateEntity
        {
            Id = templateId,
            Name = "Original Name",
            Description = "Original Description",
            Prompt = "Original Prompt",
            Category = "Original Category",
            DisplayOrder = 1,
            IsActive = true,
            IsSystem = false
        };

        _repositoryMock
            .Setup(x => x.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTemplate);

        _repositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<PersonalityTemplateEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersonalityTemplateEntity t, CancellationToken _) => t);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.Name);
        Assert.Equal("Updated Description", result.Description);
        Assert.Equal("Updated Prompt", result.Prompt);
        Assert.Equal("Updated Category", result.Category);
        Assert.Equal(5, result.DisplayOrder);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTemplateNotFound()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var command = new UpdateTemplateCommand(
            Id: templateId,
            Name: "Test",
            Description: "Test",
            Prompt: "Test",
            Category: "Test",
            DisplayOrder: 1,
            IsActive: true
        );

        _repositoryMock
            .Setup(x => x.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersonalityTemplateEntity?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Null(result);
        _repositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<PersonalityTemplateEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DoesNotUpdateCriticalProperties_ForSystemTemplates()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var command = new UpdateTemplateCommand(
            Id: templateId,
            Name: "New Name",
            Description: "New Description",
            Prompt: "New Prompt",
            Category: "New Category",
            DisplayOrder: 10,
            IsActive: false
        );

        var systemTemplate = new PersonalityTemplateEntity
        {
            Id = templateId,
            Name = "System Name",
            Description = "System Description",
            Prompt = "System Prompt",
            Category = "System Category",
            DisplayOrder = 1,
            IsActive = true,
            IsSystem = true // System template
        };

        PersonalityTemplateEntity? updatedTemplate = null;
        _repositoryMock
            .Setup(x => x.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemTemplate);

        _repositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<PersonalityTemplateEntity>(), It.IsAny<CancellationToken>()))
            .Callback<PersonalityTemplateEntity, CancellationToken>((t, _) => updatedTemplate = t)
            .ReturnsAsync((PersonalityTemplateEntity t, CancellationToken _) => t);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(updatedTemplate);
        // Critical properties should NOT be updated for system templates
        Assert.Equal("System Name", updatedTemplate.Name);
        Assert.Equal("System Description", updatedTemplate.Description);
        Assert.Equal("System Prompt", updatedTemplate.Prompt);
        Assert.Equal("System Category", updatedTemplate.Category);
        // These CAN be updated even for system templates
        Assert.Equal(10, updatedTemplate.DisplayOrder);
        Assert.False(updatedTemplate.IsActive);
    }

    [Fact]
    public async Task Handle_UpdatesAllProperties_ForNonSystemTemplates()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var command = new UpdateTemplateCommand(
            Id: templateId,
            Name: "New Name",
            Description: "New Description",
            Prompt: "New Prompt",
            Category: "New Category",
            DisplayOrder: 10,
            IsActive: false
        );

        var userTemplate = new PersonalityTemplateEntity
        {
            Id = templateId,
            Name = "Old Name",
            Description = "Old Description",
            Prompt = "Old Prompt",
            Category = "Old Category",
            DisplayOrder = 1,
            IsActive = true,
            IsSystem = false // User template
        };

        PersonalityTemplateEntity? updatedTemplate = null;
        _repositoryMock
            .Setup(x => x.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userTemplate);

        _repositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<PersonalityTemplateEntity>(), It.IsAny<CancellationToken>()))
            .Callback<PersonalityTemplateEntity, CancellationToken>((t, _) => updatedTemplate = t)
            .ReturnsAsync((PersonalityTemplateEntity t, CancellationToken _) => t);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(updatedTemplate);
        Assert.Equal("New Name", updatedTemplate.Name);
        Assert.Equal("New Description", updatedTemplate.Description);
        Assert.Equal("New Prompt", updatedTemplate.Prompt);
        Assert.Equal("New Category", updatedTemplate.Category);
        Assert.Equal(10, updatedTemplate.DisplayOrder);
        Assert.False(updatedTemplate.IsActive);
    }

    [Fact]
    public async Task Handle_UpdatesUpdatedAtTimestamp()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var originalUpdatedAt = DateTime.UtcNow.AddDays(-1);
        var command = new UpdateTemplateCommand(
            Id: templateId,
            Name: "Test",
            Description: "Test",
            Prompt: "Test",
            Category: "Test",
            DisplayOrder: 1,
            IsActive: true
        );

        var existingTemplate = new PersonalityTemplateEntity
        {
            Id = templateId,
            UpdatedAt = originalUpdatedAt,
            IsSystem = false
        };

        var beforeUpdate = DateTime.UtcNow;
        PersonalityTemplateEntity? updatedTemplate = null;

        _repositoryMock
            .Setup(x => x.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTemplate);

        _repositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<PersonalityTemplateEntity>(), It.IsAny<CancellationToken>()))
            .Callback<PersonalityTemplateEntity, CancellationToken>((t, _) => updatedTemplate = t)
            .ReturnsAsync((PersonalityTemplateEntity t, CancellationToken _) => t);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        var afterUpdate = DateTime.UtcNow;

        // Assert
        Assert.NotNull(updatedTemplate);
        Assert.True(updatedTemplate.UpdatedAt >= beforeUpdate);
        Assert.True(updatedTemplate.UpdatedAt <= afterUpdate);
        Assert.True(updatedTemplate.UpdatedAt > originalUpdatedAt);
    }
}
