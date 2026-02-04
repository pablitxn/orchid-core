using Application.Interfaces;
using Application.UseCases.OpenApiPlugin.CreateOpenApiPlugin;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.OpenApiPlugin;

public class CreateOpenApiPluginHandlerTests
{
    private readonly CreateOpenApiPluginHandler _handler;
    private readonly Mock<IPluginRepository> _repo = new();
    private readonly Mock<IUserPluginRepository> _userPluginRepo = new();
    private readonly Mock<ILogger<CreateOpenApiPluginHandler>> _logger = new();
    private readonly Guid _creatorUserId = Guid.NewGuid();

    public CreateOpenApiPluginHandlerTests()
    {
        _handler = new CreateOpenApiPluginHandler(_repo.Object, _userPluginRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_CreatesOpenApiPlugin()
    {
        var cmd = new CreateOpenApiPluginCommand(
            _creatorUserId,
            "Test Plugin",
            "test_plugin",
            "https://api.example.com",
            "A test plugin");

        _repo.Setup(r => r.GetBySystemNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PluginEntity?)null);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        _repo.Verify(r => r.CreateAsync(
            It.Is<PluginEntity>(p =>
                p.Name == cmd.Name &&
                p.SystemName == cmd.SystemName &&
                p.PluginType == "openapi" &&
                p.BaseUrl == cmd.BaseUrl),
            It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal(cmd.Name, result.Name);
        Assert.Equal(cmd.SystemName, result.SystemName);
        Assert.Equal(cmd.BaseUrl, result.BaseUrl);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task Handle_GrantsAccessToCreator()
    {
        var cmd = new CreateOpenApiPluginCommand(
            _creatorUserId,
            "Test Plugin",
            "test_plugin",
            "https://api.example.com");

        _repo.Setup(r => r.GetBySystemNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PluginEntity?)null);

        await _handler.Handle(cmd, CancellationToken.None);

        _userPluginRepo.Verify(r => r.CreateAsync(
            It.Is<UserPluginEntity>(up =>
                up.UserId == _creatorUserId &&
                up.IsActive),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsValidationException_WhenSystemNameExists()
    {
        var existingPlugin = new PluginEntity { SystemName = "test_plugin" };
        _repo.Setup(r => r.GetBySystemNameAsync("test_plugin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPlugin);

        var cmd = new CreateOpenApiPluginCommand(
            _creatorUserId,
            "Test Plugin",
            "test_plugin",
            "https://api.example.com");

        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(cmd, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ThrowsValidationException_WhenNameEmpty(string name)
    {
        var cmd = new CreateOpenApiPluginCommand(
            _creatorUserId,
            name,
            "test_plugin",
            "https://api.example.com");

        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(cmd, CancellationToken.None));
    }

    [Theory]
    [InlineData("TestPlugin")]
    [InlineData("test-plugin")]
    [InlineData("123test")]
    [InlineData("test plugin")]
    public async Task Handle_ThrowsValidationException_WhenSystemNameInvalid(string systemName)
    {
        var cmd = new CreateOpenApiPluginCommand(
            _creatorUserId,
            "Test Plugin",
            systemName,
            "https://api.example.com");

        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(cmd, CancellationToken.None));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("")]
    public async Task Handle_ThrowsValidationException_WhenBaseUrlInvalid(string baseUrl)
    {
        var cmd = new CreateOpenApiPluginCommand(
            _creatorUserId,
            "Test Plugin",
            "test_plugin",
            baseUrl);

        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsValidationException_WhenOpenApiSpecUrlDoesNotStartWithSlash()
    {
        var cmd = new CreateOpenApiPluginCommand(
            _creatorUserId,
            "Test Plugin",
            "test_plugin",
            "https://api.example.com",
            OpenApiSpecUrl: "openapi.json");

        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(cmd, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(121)]
    [InlineData(1000)]
    public async Task Handle_ThrowsValidationException_WhenTimeoutOutOfRange(int timeout)
    {
        var cmd = new CreateOpenApiPluginCommand(
            _creatorUserId,
            "Test Plugin",
            "test_plugin",
            "https://api.example.com",
            TimeoutSeconds: timeout);

        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SetsDefaultValues()
    {
        var cmd = new CreateOpenApiPluginCommand(
            _creatorUserId,
            "Test Plugin",
            "test_plugin",
            "https://api.example.com");

        _repo.Setup(r => r.GetBySystemNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PluginEntity?)null);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("/openapi.json", result.OpenApiSpecUrl);
        Assert.Equal(30, result.TimeoutSeconds);
        Assert.Equal(10, result.PriceCredits);
        Assert.False(result.IsSubscriptionBased);
    }
}
