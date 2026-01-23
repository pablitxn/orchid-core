using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.UseCases.PersonalityTemplate.Common;
using Application.UseCases.PersonalityTemplate.CreateTemplate;
using Application.UseCases.PersonalityTemplate.DeleteTemplate;
using Application.UseCases.PersonalityTemplate.GetTemplate;
using Application.UseCases.PersonalityTemplate.ListTemplates;
using Application.UseCases.PersonalityTemplate.UpdateTemplate;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class PersonalityTemplatesControllerTests
{
    private readonly PersonalityTemplatesController _controller;
    private readonly Mock<IMediator> _mediatorMock;

    public PersonalityTemplatesControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new PersonalityTemplatesController(_mediatorMock.Object);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task ListTemplates_ReturnsOk_WithAllTemplates_WhenIncludeInactiveIsFalse()
    {
        // Arrange
        var expectedTemplates = new List<PersonalityTemplateDto>
        {
            new PersonalityTemplateDto
            {
                Id = Guid.NewGuid(),
                Name = "Template 1",
                IsActive = true
            },
            new PersonalityTemplateDto
            {
                Id = Guid.NewGuid(),
                Name = "Template 2",
                IsActive = true
            }
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<ListTemplatesQuery>(q => q.IncludeInactive == false),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTemplates);

        // Act
        var result = await _controller.ListTemplates(false);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var actualTemplates = Assert.IsAssignableFrom<IEnumerable<PersonalityTemplateDto>>(okResult.Value);
        Assert.Equal(2, actualTemplates.Count());
        
        _mediatorMock.Verify(m => m.Send(
            It.Is<ListTemplatesQuery>(q => q.IncludeInactive == false),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task ListTemplates_ReturnsOk_WithAllTemplates_WhenIncludeInactiveIsTrue()
    {
        // Arrange
        var expectedTemplates = new List<PersonalityTemplateDto>
        {
            new PersonalityTemplateDto
            {
                Id = Guid.NewGuid(),
                Name = "Active Template",
                IsActive = true
            },
            new PersonalityTemplateDto
            {
                Id = Guid.NewGuid(),
                Name = "Inactive Template",
                IsActive = false
            }
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<ListTemplatesQuery>(q => q.IncludeInactive == true),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTemplates);

        // Act
        var result = await _controller.ListTemplates(true);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var actualTemplates = Assert.IsAssignableFrom<IEnumerable<PersonalityTemplateDto>>(okResult.Value);
        Assert.Equal(2, actualTemplates.Count());
        Assert.Contains(actualTemplates, t => !t.IsActive);
    }

    [Fact]
    public async Task GetTemplate_ReturnsOk_WithTemplate_WhenTemplateExists()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var expectedTemplate = new PersonalityTemplateDto
        {
            Id = templateId,
            Name = "Test Template",
            Description = "Test Description",
            IsActive = true
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetTemplateQuery>(q => q.Id == templateId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTemplate);

        // Act
        var result = await _controller.GetTemplate(templateId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var actualTemplate = Assert.IsType<PersonalityTemplateDto>(okResult.Value);
        Assert.Equal(templateId, actualTemplate.Id);
        Assert.Equal("Test Template", actualTemplate.Name);
        
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetTemplateQuery>(q => q.Id == templateId),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task GetTemplate_ReturnsNotFound_WhenTemplateDoesNotExist()
    {
        // Arrange
        var templateId = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetTemplateQuery>(q => q.Id == templateId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersonalityTemplateDto?)null);

        // Act
        var result = await _controller.GetTemplate(templateId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
        
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetTemplateQuery>(q => q.Id == templateId),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task CreateTemplate_ReturnsCreatedAtAction_WithNewTemplate()
    {
        // Arrange
        var command = new CreateTemplateCommand(
            Name: "New Template",
            Description: "New Description",
            Prompt: "Test prompt",
            Category: "Test Category"
        );
        
        var createdTemplate = new PersonalityTemplateDto
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            Description = command.Description,
            Prompt = command.Prompt,
            Category = command.Category,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<CreateTemplateCommand>(c => 
                c.Name == command.Name),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTemplate);

        // Act
        var result = await _controller.CreateTemplate(command);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(_controller.GetTemplate), createdResult.ActionName);
        Assert.Equal(createdTemplate.Id, ((PersonalityTemplateDto)createdResult.Value!).Id);
        
        _mediatorMock.Verify(m => m.Send(
            It.Is<CreateTemplateCommand>(c => c.Name == command.Name),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task UpdateTemplate_ReturnsOk_WithUpdatedTemplate_WhenTemplateExists()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var command = new UpdateTemplateCommand(
            Id: templateId,
            Name: "Updated Template",
            Description: "Updated Description",
            Prompt: "Updated Prompt",
            Category: "Updated Category",
            DisplayOrder: 1,
            IsActive: true
        );
        
        var updatedTemplate = new PersonalityTemplateDto
        {
            Id = templateId,
            Name = command.Name,
            Description = command.Description,
            Prompt = command.Prompt,
            Category = command.Category,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<UpdateTemplateCommand>(c => 
                c.Id == templateId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedTemplate);

        // Act
        var result = await _controller.UpdateTemplate(templateId, command);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var actualTemplate = Assert.IsType<PersonalityTemplateDto>(okResult.Value);
        Assert.Equal(templateId, actualTemplate.Id);
        Assert.Equal("Updated Template", actualTemplate.Name);
        
        _mediatorMock.Verify(m => m.Send(
            It.Is<UpdateTemplateCommand>(c => c.Id == templateId),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task UpdateTemplate_ReturnsNotFound_WhenTemplateDoesNotExist()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var command = new UpdateTemplateCommand(
            Id: templateId,
            Name: "Updated Template",
            Description: "Description",
            Prompt: "Prompt",
            Category: "Category",
            DisplayOrder: 1,
            IsActive: true
        );

        _mediatorMock
            .Setup(m => m.Send(It.Is<UpdateTemplateCommand>(c => 
                c.Id == templateId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersonalityTemplateDto?)null);

        // Act
        var result = await _controller.UpdateTemplate(templateId, command);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateTemplate_ReturnsBadRequest_WhenIdMismatch()
    {
        // Arrange
        var urlId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var command = new UpdateTemplateCommand(
            Id: commandId,
            Name: "Test",
            Description: "Description",
            Prompt: "Prompt",
            Category: "Category",
            DisplayOrder: 1,
            IsActive: true
        );

        // Act
        var result = await _controller.UpdateTemplate(urlId, command);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("ID mismatch", badRequestResult.Value);
        
        _mediatorMock.Verify(m => m.Send(
            It.IsAny<UpdateTemplateCommand>(),
            It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Fact]
    public async Task DeleteTemplate_ReturnsNoContent_WhenTemplateExists()
    {
        // Arrange
        var templateId = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.Is<DeleteTemplateCommand>(c => 
                c.Id == templateId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteTemplate(templateId);

        // Assert
        Assert.IsType<NoContentResult>(result);
        
        _mediatorMock.Verify(m => m.Send(
            It.Is<DeleteTemplateCommand>(c => c.Id == templateId),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task DeleteTemplate_ReturnsNotFound_WhenTemplateDoesNotExist()
    {
        // Arrange
        var templateId = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.Is<DeleteTemplateCommand>(c => 
                c.Id == templateId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteTemplate(templateId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
        
        _mediatorMock.Verify(m => m.Send(
            It.Is<DeleteTemplateCommand>(c => c.Id == templateId),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}
