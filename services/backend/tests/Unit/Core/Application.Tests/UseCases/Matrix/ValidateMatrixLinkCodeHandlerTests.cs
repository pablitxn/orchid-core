using Application.Interfaces;
using Application.UseCases.Matrix.ValidateLinkCode;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.Matrix;

public class ValidateMatrixLinkCodeHandlerTests
{
    private readonly ValidateMatrixLinkCodeHandler _handler;
    private readonly Mock<IMatrixLinkCodeRepository> _linkCodeRepo = new();
    private readonly Mock<IMatrixUserLinkRepository> _userLinkRepo = new();

    public ValidateMatrixLinkCodeHandlerTests()
    {
        _handler = new ValidateMatrixLinkCodeHandler(
            _linkCodeRepo.Object,
            _userLinkRepo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsInvalidCode_WhenCodeNotFound()
    {
        var command = new ValidateMatrixLinkCodeCommand("@pablo:matrix.orchid-labs.xyz", "ORCHID-ABCD");

        _linkCodeRepo.Setup(r => r.GetByCodeAsync(command.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixLinkCodeEntity?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_CODE", result.ErrorCode);
        Assert.Null(result.UserId);
        Assert.Null(result.UserName);
        _userLinkRepo.Verify(r => r.CreateAsync(It.IsAny<MatrixUserLinkEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsCodeAlreadyUsed_WhenCodeWasUsed()
    {
        var userId = Guid.NewGuid();
        var command = new ValidateMatrixLinkCodeCommand("@pablo:matrix.orchid-labs.xyz", "ORCHID-ABCD");
        var existingCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = command.Code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            UsedAt = DateTime.UtcNow.AddMinutes(-1) // Already used
        };

        _linkCodeRepo.Setup(r => r.GetByCodeAsync(command.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCode);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("CODE_ALREADY_USED", result.ErrorCode);
        _userLinkRepo.Verify(r => r.CreateAsync(It.IsAny<MatrixUserLinkEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsExpiredCode_WhenCodeIsExpired()
    {
        var userId = Guid.NewGuid();
        var command = new ValidateMatrixLinkCodeCommand("@pablo:matrix.orchid-labs.xyz", "ORCHID-ABCD");
        var existingCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = command.Code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1) // Expired
        };

        _linkCodeRepo.Setup(r => r.GetByCodeAsync(command.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCode);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("EXPIRED_CODE", result.ErrorCode);
        _userLinkRepo.Verify(r => r.CreateAsync(It.IsAny<MatrixUserLinkEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsAlreadyLinked_WhenMatrixUserAlreadyLinked()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var matrixUserId = "@pablo:matrix.orchid-labs.xyz";
        var command = new ValidateMatrixLinkCodeCommand(matrixUserId, "ORCHID-ABCD");

        var existingCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = command.Code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        var existingLink = new MatrixUserLinkEntity
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            MatrixUserId = matrixUserId,
            IsActive = true
        };

        _linkCodeRepo.Setup(r => r.GetByCodeAsync(command.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCode);
        _userLinkRepo.Setup(r => r.GetByMatrixUserIdAsync(matrixUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLink);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ALREADY_LINKED", result.ErrorCode);
        _userLinkRepo.Verify(r => r.CreateAsync(It.IsAny<MatrixUserLinkEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenCodeIsValid()
    {
        var userId = Guid.NewGuid();
        var matrixUserId = "@pablo:matrix.orchid-labs.xyz";
        var userName = "Pablo";
        var command = new ValidateMatrixLinkCodeCommand(matrixUserId, "ORCHID-ABCD");

        var user = new UserEntity { Id = userId, Name = userName, Email = "pablo@example.com" };
        var existingCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = command.Code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        // Set user via reflection since it has private setter
        typeof(MatrixLinkCodeEntity).GetProperty("User")!.SetValue(existingCode, user);

        _linkCodeRepo.Setup(r => r.GetByCodeAsync(command.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCode);
        _userLinkRepo.Setup(r => r.GetByMatrixUserIdAsync(matrixUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixUserLinkEntity?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(userName, result.UserName);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task Handle_MarksCodeAsUsed_OnSuccess()
    {
        var userId = Guid.NewGuid();
        var matrixUserId = "@pablo:matrix.orchid-labs.xyz";
        var command = new ValidateMatrixLinkCodeCommand(matrixUserId, "ORCHID-ABCD");

        var user = new UserEntity { Id = userId, Name = "Pablo" };
        var existingCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = command.Code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        typeof(MatrixLinkCodeEntity).GetProperty("User")!.SetValue(existingCode, user);

        _linkCodeRepo.Setup(r => r.GetByCodeAsync(command.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCode);
        _userLinkRepo.Setup(r => r.GetByMatrixUserIdAsync(matrixUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixUserLinkEntity?)null);

        await _handler.Handle(command, CancellationToken.None);

        Assert.True(existingCode.IsUsed);
        _linkCodeRepo.Verify(r => r.UpdateAsync(existingCode, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CreatesLink_WithCorrectData()
    {
        var userId = Guid.NewGuid();
        var matrixUserId = "@pablo:matrix.orchid-labs.xyz";
        var command = new ValidateMatrixLinkCodeCommand(matrixUserId, "ORCHID-ABCD");
        MatrixUserLinkEntity? createdLink = null;

        var user = new UserEntity { Id = userId, Name = "Pablo" };
        var existingCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = command.Code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        typeof(MatrixLinkCodeEntity).GetProperty("User")!.SetValue(existingCode, user);

        _linkCodeRepo.Setup(r => r.GetByCodeAsync(command.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCode);
        _userLinkRepo.Setup(r => r.GetByMatrixUserIdAsync(matrixUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixUserLinkEntity?)null);
        _userLinkRepo.Setup(r => r.CreateAsync(It.IsAny<MatrixUserLinkEntity>(), It.IsAny<CancellationToken>()))
            .Callback<MatrixUserLinkEntity, CancellationToken>((link, _) => createdLink = link);

        await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(createdLink);
        Assert.Equal(userId, createdLink.UserId);
        Assert.Equal(matrixUserId, createdLink.MatrixUserId);
        Assert.True(createdLink.IsActive);
        Assert.NotEqual(Guid.Empty, createdLink.Id);
    }

    [Fact]
    public async Task Handle_AllowsLinking_WhenPreviousLinkIsInactive()
    {
        var userId = Guid.NewGuid();
        var matrixUserId = "@pablo:matrix.orchid-labs.xyz";
        var command = new ValidateMatrixLinkCodeCommand(matrixUserId, "ORCHID-ABCD");

        var user = new UserEntity { Id = userId, Name = "Pablo" };
        var existingCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = command.Code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        typeof(MatrixLinkCodeEntity).GetProperty("User")!.SetValue(existingCode, user);

        var inactiveLink = new MatrixUserLinkEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            MatrixUserId = matrixUserId,
            IsActive = false // Inactive
        };

        _linkCodeRepo.Setup(r => r.GetByCodeAsync(command.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCode);
        _userLinkRepo.Setup(r => r.GetByMatrixUserIdAsync(matrixUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveLink);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userLinkRepo.Verify(r => r.CreateAsync(It.IsAny<MatrixUserLinkEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UsesEmailAsUserName_WhenNameIsNull()
    {
        var userId = Guid.NewGuid();
        var matrixUserId = "@pablo:matrix.orchid-labs.xyz";
        var userEmail = "pablo@example.com";
        var command = new ValidateMatrixLinkCodeCommand(matrixUserId, "ORCHID-ABCD");

        var user = new UserEntity { Id = userId, Name = null, Email = userEmail };
        var existingCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = command.Code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        typeof(MatrixLinkCodeEntity).GetProperty("User")!.SetValue(existingCode, user);

        _linkCodeRepo.Setup(r => r.GetByCodeAsync(command.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCode);
        _userLinkRepo.Setup(r => r.GetByMatrixUserIdAsync(matrixUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixUserLinkEntity?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userEmail, result.UserName);
    }

    [Fact]
    public async Task Handle_ReturnsUnknown_WhenUserDataIsNull()
    {
        var userId = Guid.NewGuid();
        var matrixUserId = "@pablo:matrix.orchid-labs.xyz";
        var command = new ValidateMatrixLinkCodeCommand(matrixUserId, "ORCHID-ABCD");

        var existingCode = new MatrixLinkCodeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = command.Code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        // User is null (not loaded)

        _linkCodeRepo.Setup(r => r.GetByCodeAsync(command.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCode);
        _userLinkRepo.Setup(r => r.GetByMatrixUserIdAsync(matrixUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MatrixUserLinkEntity?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Unknown", result.UserName);
    }
}
