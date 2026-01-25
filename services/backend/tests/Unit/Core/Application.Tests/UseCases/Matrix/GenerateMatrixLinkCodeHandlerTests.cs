using Application.Interfaces;
using Application.UseCases.Matrix.GenerateLinkCode;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.Matrix;

public class GenerateMatrixLinkCodeHandlerTests
{
    private readonly GenerateMatrixLinkCodeHandler _handler;
    private readonly Mock<IMatrixUserLinkRepository> _userLinkRepo = new();
    private readonly Mock<IMatrixLinkCodeRepository> _linkCodeRepo = new();

    public GenerateMatrixLinkCodeHandlerTests()
    {
        _handler = new GenerateMatrixLinkCodeHandler(
            _userLinkRepo.Object,
            _linkCodeRepo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsAlreadyLinked_WhenUserHasActiveLink()
    {
        var userId = Guid.NewGuid();
        var matrixUserId = "@pablo:matrix.orchid-labs.xyz";
        var existingLink = new MatrixUserLinkEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MatrixUserId = matrixUserId,
            IsActive = true
        };

        _userLinkRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLink);

        var result = await _handler.Handle(new GenerateMatrixLinkCodeCommand(userId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ALREADY_LINKED", result.ErrorCode);
        Assert.Equal(matrixUserId, result.MatrixUserId);
        _linkCodeRepo.Verify(r => r.CreateAsync(It.IsAny<MatrixLinkCodeEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_GeneratesCode_WhenUserNotLinked()
    {
        var userId = Guid.NewGuid();
        _userLinkRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixUserLinkEntity?)null);
        _linkCodeRepo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixLinkCodeEntity?)null);
        _linkCodeRepo.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(new GenerateMatrixLinkCodeCommand(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Code);
        Assert.StartsWith("ORCHID-", result.Code);
        Assert.Equal(11, result.Code.Length); // ORCHID- (7) + 4 chars
        Assert.NotNull(result.ExpiresAt);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
        _linkCodeRepo.Verify(r => r.CreateAsync(It.IsAny<MatrixLinkCodeEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidatesPreviousCode_WhenExists()
    {
        var userId = Guid.NewGuid();
        var existingCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = "ORCHID-ABCD",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        _userLinkRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixUserLinkEntity?)null);
        _linkCodeRepo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCode);
        _linkCodeRepo.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _handler.Handle(new GenerateMatrixLinkCodeCommand(userId), CancellationToken.None);

        Assert.True(existingCode.IsUsed);
        _linkCodeRepo.Verify(r => r.UpdateAsync(existingCode, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GeneratesUniqueCode_WhenFirstAttemptExists()
    {
        var userId = Guid.NewGuid();
        var callCount = 0;

        _userLinkRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixUserLinkEntity?)null);
        _linkCodeRepo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixLinkCodeEntity?)null);
        _linkCodeRepo.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1; // First code exists, second doesn't
            });

        var result = await _handler.Handle(new GenerateMatrixLinkCodeCommand(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _linkCodeRepo.Verify(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_SetsCorrectExpiration()
    {
        var userId = Guid.NewGuid();
        MatrixLinkCodeEntity? createdCode = null;

        _userLinkRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixUserLinkEntity?)null);
        _linkCodeRepo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixLinkCodeEntity?)null);
        _linkCodeRepo.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _linkCodeRepo.Setup(r => r.CreateAsync(It.IsAny<MatrixLinkCodeEntity>(), It.IsAny<CancellationToken>()))
            .Callback<MatrixLinkCodeEntity, CancellationToken>((code, _) => createdCode = code);

        var beforeCall = DateTime.UtcNow;
        await _handler.Handle(new GenerateMatrixLinkCodeCommand(userId), CancellationToken.None);
        var afterCall = DateTime.UtcNow;

        Assert.NotNull(createdCode);
        var expectedMinExpiration = beforeCall.AddMinutes(MatrixLinkCodeEntity.ExpirationMinutes);
        var expectedMaxExpiration = afterCall.AddMinutes(MatrixLinkCodeEntity.ExpirationMinutes);
        Assert.True(createdCode.ExpiresAt >= expectedMinExpiration);
        Assert.True(createdCode.ExpiresAt <= expectedMaxExpiration);
    }

    [Fact]
    public async Task Handle_AllowsGeneration_WhenLinkIsInactive()
    {
        var userId = Guid.NewGuid();
        var inactiveLink = new MatrixUserLinkEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MatrixUserId = "@old:matrix.orchid-labs.xyz",
            IsActive = false
        };

        _userLinkRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveLink);
        _linkCodeRepo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixLinkCodeEntity?)null);
        _linkCodeRepo.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(new GenerateMatrixLinkCodeCommand(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Code);
    }

    [Fact]
    public async Task Handle_CodeFormatIsValid()
    {
        var userId = Guid.NewGuid();
        MatrixLinkCodeEntity? createdCode = null;

        _userLinkRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixUserLinkEntity?)null);
        _linkCodeRepo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixLinkCodeEntity?)null);
        _linkCodeRepo.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _linkCodeRepo.Setup(r => r.CreateAsync(It.IsAny<MatrixLinkCodeEntity>(), It.IsAny<CancellationToken>()))
            .Callback<MatrixLinkCodeEntity, CancellationToken>((code, _) => createdCode = code);

        await _handler.Handle(new GenerateMatrixLinkCodeCommand(userId), CancellationToken.None);

        Assert.NotNull(createdCode);
        Assert.Matches(@"^ORCHID-[A-Z0-9]{4}$", createdCode.Code);
    }

    [Fact]
    public async Task Handle_SetsCorrectUserId()
    {
        var userId = Guid.NewGuid();
        MatrixLinkCodeEntity? createdCode = null;

        _userLinkRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixUserLinkEntity?)null);
        _linkCodeRepo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixLinkCodeEntity?)null);
        _linkCodeRepo.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _linkCodeRepo.Setup(r => r.CreateAsync(It.IsAny<MatrixLinkCodeEntity>(), It.IsAny<CancellationToken>()))
            .Callback<MatrixLinkCodeEntity, CancellationToken>((code, _) => createdCode = code);

        await _handler.Handle(new GenerateMatrixLinkCodeCommand(userId), CancellationToken.None);

        Assert.NotNull(createdCode);
        Assert.Equal(userId, createdCode.UserId);
    }
}
