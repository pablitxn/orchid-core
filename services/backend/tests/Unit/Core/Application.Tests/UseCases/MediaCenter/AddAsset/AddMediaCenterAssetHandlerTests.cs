using Application.Interfaces;
using Application.UseCases.MediaCenter.AddAsset;
using Core.Application.Interfaces;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.MediaCenter.AddAsset;

public class AddMediaCenterAssetHandlerTests
{
    [Fact]
    public async Task Handle_PersistsAsset()
    {
        var repo = new Mock<IMediaCenterAssetRepository>();
        var handler = new AddMediaCenterAssetHandler(repo.Object);
        var cmd = new AddMediaCenterAssetCommand(Guid.NewGuid(), "image/png", "pic", "url", null);

        var asset = await handler.Handle(cmd, CancellationToken.None);

        repo.Verify(r => r.AddAsync(It.IsAny<MediaCenterAssetEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(cmd.MimeType, asset.MimeType);
    }
}