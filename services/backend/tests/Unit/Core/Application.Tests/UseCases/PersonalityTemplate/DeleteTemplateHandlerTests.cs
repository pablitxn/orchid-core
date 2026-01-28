using Application.Interfaces;
using Application.UseCases.PersonalityTemplate.DeleteTemplate;
using Core.Domain.Entities;
using Domain.Exceptions;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.PersonalityTemplate;

public class DeleteTemplateHandlerTests
{
    private readonly Mock<IPersonalityTemplateRepository> _repositoryMock;
    private readonly DeleteTemplateHandler _handler;

    public DeleteTemplateHandlerTests()
    {
        _repositoryMock = new Mock<IPersonalityTemplateRepository>();
        _handler = new DeleteTemplateHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_DeletesTemplate_WhenTemplateExists()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var command = new DeleteTemplateCommand(templateId);

        var existingTemplate = new PersonalityTemplateEntity
        {
            Id = templateId,
            Name = "Template to Delete"
        };

        _repositoryMock
            .Setup(x => x.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTemplate);

        _repositoryMock
            .Setup(x => x.DeleteAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(MediatR.Unit.Value, result);
        _repositoryMock.Verify(x => x.DeleteAsync(templateId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFoundException_WhenTemplateNotFound()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var command = new DeleteTemplateCommand(templateId);

        _repositoryMock
            .Setup(x => x.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersonalityTemplateEntity?)null);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _repositoryMock.Verify(
            x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_CallsGetByIdFirst_ThenDelete()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var command = new DeleteTemplateCommand(templateId);
        var callOrder = new List<string>();

        var existingTemplate = new PersonalityTemplateEntity
        {
            Id = templateId
        };

        _repositoryMock
            .Setup(x => x.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("GetById"))
            .ReturnsAsync(existingTemplate);

        _repositoryMock
            .Setup(x => x.DeleteAsync(templateId, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("Delete"))
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(2, callOrder.Count);
        Assert.Equal("GetById", callOrder[0]);
        Assert.Equal("Delete", callOrder[1]);
    }

    [Fact]
    public async Task Handle_DeletesWithCorrectId()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var command = new DeleteTemplateCommand(templateId);

        var existingTemplate = new PersonalityTemplateEntity
        {
            Id = templateId
        };

        Guid? deletedId = null;
        _repositoryMock
            .Setup(x => x.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTemplate);

        _repositoryMock
            .Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((id, _) => deletedId = id)
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(templateId, deletedId);
    }
}
