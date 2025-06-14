using Application.Interfaces;
using Application.UseCases.MediaCenter.ListAssets;
using Core.Application.Interfaces;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.MediaCenter.ListAssets;

public class ListMediaCenterAssetsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAssets()
    {
        var repo = new Mock<IMediaCenterAssetRepository>();
        repo.Setup(r => r.SearchAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaCenterAssetEntity> { new() { Id = Guid.NewGuid() } });
        var handler = new ListMediaCenterAssetsHandler(repo.Object);

        var result = await handler.Handle(new ListMediaCenterAssetsQuery(null), CancellationToken.None);

        Assert.Single(result);
    }
}