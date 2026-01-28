using FluentAssertions;
using Infrastructure.Cache;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Tests.Cache;

public class InMemoryCacheServiceTests
{
    private readonly InMemoryCacheService _service;
    private readonly IMemoryCache _memoryCache;

    public InMemoryCacheServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _service = new InMemoryCacheService(_memoryCache);
    }

    [Fact]
    public async Task GetStringAsync_Should_ReturnNull_When_KeyNotFound()
    {
        // Arrange
        var key = "non-existent-key";

        // Act
        var result = await _service.GetStringAsync(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetStringAsync_Should_ReturnValue_When_KeyExists()
    {
        // Arrange
        var key = "existing-key";
        var value = "test-value";
        await _service.SetStringAsync(key, value);

        // Act
        var result = await _service.GetStringAsync(key);

        // Assert
        result.Should().Be(value);
    }

    [Fact]
    public async Task GetStringAsync_Should_ThrowException_When_KeyIsNull()
    {
        // Act
        var act = () => _service.GetStringAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public async Task GetStringAsync_Should_ThrowException_When_KeyIsEmpty()
    {
        // Act
        var act = () => _service.GetStringAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public async Task SetStringAsync_Should_StoreValue()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";

        // Act
        await _service.SetStringAsync(key, value);

        // Assert
        var result = await _service.GetStringAsync(key);
        result.Should().Be(value);
    }

    [Fact]
    public async Task SetStringAsync_Should_ThrowException_When_KeyIsNull()
    {
        // Act
        var act = () => _service.SetStringAsync(null!, "value");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public async Task SetStringAsync_Should_ThrowException_When_KeyIsEmpty()
    {
        // Act
        var act = () => _service.SetStringAsync(string.Empty, "value");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public async Task SetStringAsync_Should_ThrowException_When_ValueIsNull()
    {
        // Act
        var act = () => _service.SetStringAsync("key", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SetStringAsync_Should_ThrowException_When_ValueIsEmpty()
    {
        // Act
        var act = () => _service.SetStringAsync("key", string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public async Task SetStringAsync_Should_OverwriteExistingValue()
    {
        // Arrange
        var key = "overwrite-key";
        await _service.SetStringAsync(key, "original-value");

        // Act
        await _service.SetStringAsync(key, "new-value");

        // Assert
        var result = await _service.GetStringAsync(key);
        result.Should().Be("new-value");
    }

    [Fact]
    public async Task SetStringAsync_Should_UseDefaultExpiration_When_NotSpecified()
    {
        // Arrange
        var key = "default-expiry-key";
        var value = "test-value";

        // Act
        await _service.SetStringAsync(key, value);

        // Assert - Value should be retrievable immediately
        var result = await _service.GetStringAsync(key);
        result.Should().Be(value);
    }

    [Fact]
    public async Task SetStringAsync_Should_UseCustomExpiration_When_Specified()
    {
        // Arrange
        var key = "custom-expiry-key";
        var value = "test-value";
        var expiration = TimeSpan.FromMinutes(30);

        // Act
        await _service.SetStringAsync(key, value, expiration);

        // Assert - Value should be retrievable immediately
        var result = await _service.GetStringAsync(key);
        result.Should().Be(value);
    }

    [Fact]
    public async Task RemoveAsync_Should_RemoveExistingKey()
    {
        // Arrange
        var key = "remove-key";
        await _service.SetStringAsync(key, "value");

        // Act
        await _service.RemoveAsync(key);

        // Assert
        var result = await _service.GetStringAsync(key);
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_Should_NotThrow_When_KeyDoesNotExist()
    {
        // Arrange
        var key = "non-existent-remove-key";

        // Act
        var act = () => _service.RemoveAsync(key);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveAsync_Should_ThrowException_When_KeyIsNull()
    {
        // Act
        var act = () => _service.RemoveAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public async Task RemoveAsync_Should_ThrowException_When_KeyIsEmpty()
    {
        // Act
        var act = () => _service.RemoveAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public async Task SetStringAsync_Should_SupportCancellation()
    {
        // Arrange
        var key = "cancellation-key";
        var value = "test-value";
        var cts = new CancellationTokenSource();

        // Act
        await _service.SetStringAsync(key, value, cancellationToken: cts.Token);

        // Assert
        var result = await _service.GetStringAsync(key, cts.Token);
        result.Should().Be(value);
    }

    [Fact]
    public async Task MultipleKeys_Should_BeStoredIndependently()
    {
        // Arrange
        var key1 = "multi-key-1";
        var key2 = "multi-key-2";
        var value1 = "value-1";
        var value2 = "value-2";

        // Act
        await _service.SetStringAsync(key1, value1);
        await _service.SetStringAsync(key2, value2);

        // Assert
        var result1 = await _service.GetStringAsync(key1);
        var result2 = await _service.GetStringAsync(key2);

        result1.Should().Be(value1);
        result2.Should().Be(value2);
    }

    [Fact]
    public async Task RemoveAsync_Should_OnlyRemoveSpecificKey()
    {
        // Arrange
        var key1 = "keep-key";
        var key2 = "remove-specific-key";
        await _service.SetStringAsync(key1, "value1");
        await _service.SetStringAsync(key2, "value2");

        // Act
        await _service.RemoveAsync(key2);

        // Assert
        var result1 = await _service.GetStringAsync(key1);
        var result2 = await _service.GetStringAsync(key2);

        result1.Should().Be("value1");
        result2.Should().BeNull();
    }

    [Fact]
    public async Task SetStringAsync_Should_HandleLargeValues()
    {
        // Arrange
        var key = "large-value-key";
        var value = new string('x', 100000); // 100KB string

        // Act
        await _service.SetStringAsync(key, value);

        // Assert
        var result = await _service.GetStringAsync(key);
        result.Should().Be(value);
    }

    [Fact]
    public async Task SetStringAsync_Should_HandleSpecialCharactersInKey()
    {
        // Arrange
        var key = "special:key:with/slashes:and:colons";
        var value = "test-value";

        // Act
        await _service.SetStringAsync(key, value);

        // Assert
        var result = await _service.GetStringAsync(key);
        result.Should().Be(value);
    }

    [Fact]
    public async Task SetStringAsync_Should_HandleSpecialCharactersInValue()
    {
        // Arrange
        var key = "special-value-key";
        var value = "value with special chars: \n\t\r\"'<>&";

        // Act
        await _service.SetStringAsync(key, value);

        // Assert
        var result = await _service.GetStringAsync(key);
        result.Should().Be(value);
    }

    [Fact]
    public async Task SetStringAsync_Should_HandleUnicodeCharacters()
    {
        // Arrange
        var key = "unicode-key-日本語";
        var value = "Unicode value: 日本語 中文 한국어 🎉";

        // Act
        await _service.SetStringAsync(key, value);

        // Assert
        var result = await _service.GetStringAsync(key);
        result.Should().Be(value);
    }
}
