using Application.Interfaces;
using Application.UseCases.Matrix.GetUserLink;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.Matrix;

public class GetMatrixUserLinkHandlerTests
{
    private readonly GetMatrixUserLinkHandler _handler;
    private readonly Mock<IMatrixUserLinkRepository> _userLinkRepo = new();

    public GetMatrixUserLinkHandlerTests()
    {
        _handler = new GetMatrixUserLinkHandler(_userLinkRepo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNotLinked_WhenLinkNotFound()
    {
        var matrixUserId = "@pablo:matrix.orchid-labs.xyz";
        var query = new GetMatrixUserLinkQuery(matrixUserId);

        _userLinkRepo.Setup(r => r.GetByMatrixUserIdWithUserAsync(matrixUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixUserLinkEntity?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.False(result.Linked);
        Assert.Null(result.UserId);
        Assert.Null(result.UserName);
    }

    [Fact]
    public async Task Handle_ReturnsNotLinked_WhenLinkIsInactive()
    {
        var matrixUserId = "@pablo:matrix.orchid-labs.xyz";
        var query = new GetMatrixUserLinkQuery(matrixUserId);

        var inactiveLink = new MatrixUserLinkEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            MatrixUserId = matrixUserId,
            IsActive = false
        };

        _userLinkRepo.Setup(r => r.GetByMatrixUserIdWithUserAsync(matrixUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveLink);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.False(result.Linked);
        Assert.Null(result.UserId);
        Assert.Null(result.UserName);
    }

    [Fact]
    public async Task Handle_ReturnsLinked_WithUserData()
    {
        var matrixUserId = "@pablo:matrix.orchid-labs.xyz";
        var userId = Guid.NewGuid();
        var userName = "Pablo";
        var query = new GetMatrixUserLinkQuery(matrixUserId);

        var user = new UserEntity { Id = userId, Name = userName, Email = "pablo@example.com" };
        var link = new MatrixUserLinkEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MatrixUserId = matrixUserId,
            IsActive = true
        };
        typeof(MatrixUserLinkEntity).GetProperty("User")!.SetValue(link, user);

        _userLinkRepo.Setup(r => r.GetByMatrixUserIdWithUserAsync(matrixUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.Linked);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(userName, result.UserName);
    }

    [Fact]
    public async Task Handle_UsesEmailAsUserName_WhenNameIsNull()
    {
        var matrixUserId = "@pablo:matrix.orchid-labs.xyz";
        var userId = Guid.NewGuid();
        var userEmail = "pablo@example.com";
        var query = new GetMatrixUserLinkQuery(matrixUserId);

        var user = new UserEntity { Id = userId, Name = null, Email = userEmail };
        var link = new MatrixUserLinkEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MatrixUserId = matrixUserId,
            IsActive = true
        };
        typeof(MatrixUserLinkEntity).GetProperty("User")!.SetValue(link, user);

        _userLinkRepo.Setup(r => r.GetByMatrixUserIdWithUserAsync(matrixUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.Linked);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(userEmail, result.UserName);
    }

    [Fact]
    public async Task Handle_ReturnsUnknown_WhenUserDataIsNull()
    {
        var matrixUserId = "@pablo:matrix.orchid-labs.xyz";
        var userId = Guid.NewGuid();
        var query = new GetMatrixUserLinkQuery(matrixUserId);

        var link = new MatrixUserLinkEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MatrixUserId = matrixUserId,
            IsActive = true
        };
        // User is null (not loaded)

        _userLinkRepo.Setup(r => r.GetByMatrixUserIdWithUserAsync(matrixUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.Linked);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("Unknown", result.UserName);
    }
}
