using Application.UseCases.MediaCenter.Common;
using Application.UseCases.MediaCenter.ListAssets;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class MediaCenterControllerTests
{
    private readonly MediaCenterController _controller;
    private readonly Mock<IMediator> _mediatorMock;

    public MediaCenterControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new MediaCenterController(_mediatorMock.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task GetAssets_ReturnsOk_WithAllAssets_WhenNoMimeTypeFilter()
    {
        // Arrange
        var expectedAssets = new List<MediaCenterAssetDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "image1.jpg",
                MimeType = "image/jpeg",
                FileUrl = "https://example.com/image1.jpg"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = "video1.mp4",
                MimeType = "video/mp4",
                FileUrl = "https://example.com/video1.mp4"
            }
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<ListMediaCenterAssetsQuery>(q => q.MimeType == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAssets);

        // Act
        var result = await _controller.GetAssets(null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var actualAssets = Assert.IsAssignableFrom<IEnumerable<MediaCenterAssetDto>>(okResult.Value);
        Assert.Equal(2, actualAssets.Count());

        _mediatorMock.Verify(m => m.Send(
            It.Is<ListMediaCenterAssetsQuery>(q => q.MimeType == null),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAssets_ReturnsOk_WithFilteredAssets_WhenMimeTypeProvided()
    {
        // Arrange
        var mimeTypeFilter = "image/jpeg";
        var expectedAssets = new List<MediaCenterAssetDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "image1.jpg",
                MimeType = "image/jpeg",
                FileUrl = "https://example.com/image1.jpg"
            }
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<ListMediaCenterAssetsQuery>(q => q.MimeType == mimeTypeFilter),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAssets);

        // Act
        var result = await _controller.GetAssets(mimeTypeFilter);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var actualAssets = Assert.IsAssignableFrom<IEnumerable<MediaCenterAssetDto>>(okResult.Value);
        Assert.Single(actualAssets);
        Assert.All(actualAssets, asset => Assert.Equal("image/jpeg", asset.MimeType));

        _mediatorMock.Verify(m => m.Send(
            It.Is<ListMediaCenterAssetsQuery>(q => q.MimeType == mimeTypeFilter),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAssets_ReturnsOk_WithEmptyList_WhenNoAssetsFound()
    {
        // Arrange
        var expectedAssets = new List<MediaCenterAssetDto>();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ListMediaCenterAssetsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAssets);

        // Act
        var result = await _controller.GetAssets(null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var actualAssets = Assert.IsAssignableFrom<IEnumerable<MediaCenterAssetDto>>(okResult.Value);
        Assert.Empty(actualAssets);
    }
}
