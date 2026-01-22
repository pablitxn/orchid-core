using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Core.Application.DTOs.KnowledgeBase;
using Core.Application.UseCases.KnowledgeBase.DeleteKnowledgeBase;
using Core.Application.UseCases.KnowledgeBase.GetKnowledgeBaseById;
using Core.Application.UseCases.KnowledgeBase.GetKnowledgeBaseList;
using Core.Application.UseCases.KnowledgeBase.UpdateKnowledgeBase;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class KnowledgeBaseControllerTests
{
    private readonly KnowledgeBaseController _controller;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Guid _testUserId = Guid.NewGuid();

    public KnowledgeBaseControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new KnowledgeBaseController(_mediatorMock.Object);
        
        // Setup controller context with user claims
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = claimsPrincipal
            }
        };
    }

    [Fact]
    public async Task GetList_ReturnsOk_WithKnowledgeBaseList()
    {
        // Arrange
        var query = new KnowledgeBaseQueryDto
        {
            Page = 1,
            PageSize = 10,
            SearchTerm = "test"
        };
        
        var expectedResult = new KnowledgeBaseListResponseDto
        {
            Items = new List<KnowledgeBaseItemDto>
            {
                new KnowledgeBaseItemDto
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Document",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            },
            TotalCount = 1,
            Page = 1,
            PageSize = 10
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetKnowledgeBaseListQuery>(q => 
                q.Query == query && q.UserId == _testUserId), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetList(query);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var actualResult = Assert.IsType<KnowledgeBaseListResponseDto>(okResult.Value);
        Assert.Equal(expectedResult.TotalCount, actualResult.TotalCount);
        Assert.Single(actualResult.Items);
        
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetKnowledgeBaseListQuery>(q => q.UserId == _testUserId),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task GetById_ReturnsOk_WithKnowledgeBaseItem()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var expectedItem = new KnowledgeBaseItemDto
        {
            Id = itemId,
            Title = "Test Document",
            Description = "Test Description",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetKnowledgeBaseByIdQuery>(q => 
                q.Id == itemId && q.UserId == _testUserId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedItem);

        // Act
        var result = await _controller.GetById(itemId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var actualItem = Assert.IsType<KnowledgeBaseItemDto>(okResult.Value);
        Assert.Equal(itemId, actualItem.Id);
        Assert.Equal("Test Document", actualItem.Title);
        
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetKnowledgeBaseByIdQuery>(q => q.Id == itemId && q.UserId == _testUserId),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task Update_ReturnsOk_WithUpdatedItem()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var updateDto = new UpdateKnowledgeBaseDto
        {
            Title = "Updated Title",
            Description = "Updated Description"
        };
        
        var updatedItem = new KnowledgeBaseItemDto
        {
            Id = itemId,
            Title = updateDto.Title!,
            Description = updateDto.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<UpdateKnowledgeBaseCommand>(c => 
                c.Id == itemId && c.UserId == _testUserId && c.UpdateDto == updateDto),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedItem);

        // Act
        var result = await _controller.Update(itemId, updateDto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var actualItem = Assert.IsType<KnowledgeBaseItemDto>(okResult.Value);
        Assert.Equal(itemId, actualItem.Id);
        Assert.Equal("Updated Title", actualItem.Title);
        
        _mediatorMock.Verify(m => m.Send(
            It.Is<UpdateKnowledgeBaseCommand>(c => 
                c.Id == itemId && 
                c.UserId == _testUserId &&
                c.UpdateDto == updateDto),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var itemId = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.Is<DeleteKnowledgeBaseCommand>(c => 
                c.Id == itemId && c.UserId == _testUserId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        // Act
        var result = await _controller.Delete(itemId);

        // Assert
        Assert.IsType<NoContentResult>(result);
        
        _mediatorMock.Verify(m => m.Send(
            It.Is<DeleteKnowledgeBaseCommand>(c => c.Id == itemId && c.UserId == _testUserId),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task GetList_ThrowsUnauthorizedException_WhenUserIdNotInClaims()
    {
        // Arrange
        var controllerWithoutUser = new KnowledgeBaseController(_mediatorMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var query = new KnowledgeBaseQueryDto();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await controllerWithoutUser.GetList(query));
    }

    [Fact]
    public async Task GetById_ThrowsUnauthorizedException_WhenUserIdNotInClaims()
    {
        // Arrange
        var controllerWithoutUser = new KnowledgeBaseController(_mediatorMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await controllerWithoutUser.GetById(Guid.NewGuid()));
    }
}
