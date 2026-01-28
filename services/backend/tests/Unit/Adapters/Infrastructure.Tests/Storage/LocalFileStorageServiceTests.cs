using FluentAssertions;
using Infrastructure.Providers;

namespace Infrastructure.Tests.Storage;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _tempStoragePath;
    private readonly LocalFileStorageService _service;

    public LocalFileStorageServiceTests()
    {
        _tempStoragePath = Path.Combine(Path.GetTempPath(), $"LocalFileStorageTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempStoragePath);
        _service = new LocalFileStorageService(_tempStoragePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempStoragePath))
        {
            Directory.Delete(_tempStoragePath, recursive: true);
        }
    }

    [Fact]
    public async Task StoreFileAsync_Should_CreateFileOnDisk()
    {
        // Arrange
        var fileName = "test-file.txt";
        var content = "Hello, World!"u8.ToArray();
        using var stream = new MemoryStream(content);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "text/plain");

        // Assert
        File.Exists(result).Should().BeTrue();
        var storedContent = await File.ReadAllBytesAsync(result);
        storedContent.Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task StoreFileAsync_Should_ReturnFullPath()
    {
        // Arrange
        var fileName = "path-test.txt";
        var content = "Test content"u8.ToArray();
        using var stream = new MemoryStream(content);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "text/plain");

        // Assert
        result.Should().StartWith(_tempStoragePath);
        result.Should().EndWith(fileName);
    }

    [Fact]
    public async Task StoreFileAsync_Should_CreateNestedDirectories()
    {
        // Arrange
        var fileName = Path.Combine("subfolder", "nested", "deep-file.txt");
        var content = "Nested content"u8.ToArray();
        using var stream = new MemoryStream(content);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "text/plain");

        // Assert
        File.Exists(result).Should().BeTrue();
    }

    [Fact]
    public async Task GetFileAsync_Should_ReturnFileStream()
    {
        // Arrange
        var fileName = "get-test.txt";
        var content = "Read me!"u8.ToArray();
        using var writeStream = new MemoryStream(content);
        await _service.StoreFileAsync(writeStream, fileName, "text/plain");

        // Act
        await using var result = await _service.GetFileAsync(fileName, CancellationToken.None);

        // Assert
        using var reader = new MemoryStream();
        await result.CopyToAsync(reader);
        reader.ToArray().Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task GetFileAsync_Should_ThrowFileNotFoundException_When_FileDoesNotExist()
    {
        // Arrange
        var fileName = "non-existent.txt";

        // Act
        var act = () => _service.GetFileAsync(fileName, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task GetFileAsync_Overload_Should_ReturnFileStream()
    {
        // Arrange
        var fileName = "overload-test.txt";
        var content = "Overload content"u8.ToArray();
        using var writeStream = new MemoryStream(content);
        await _service.StoreFileAsync(writeStream, fileName, "text/plain");

        // Act
        await using var result = await _service.GetFileAsync(fileName);

        // Assert
        using var reader = new MemoryStream();
        await result.CopyToAsync(reader);
        reader.ToArray().Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task StoreFileAsync_Should_OverwriteExistingFile()
    {
        // Arrange
        var fileName = "overwrite.txt";
        var originalContent = "Original"u8.ToArray();
        var newContent = "New content"u8.ToArray();

        using (var stream1 = new MemoryStream(originalContent))
        {
            await _service.StoreFileAsync(stream1, fileName, "text/plain");
        }

        // Act
        using (var stream2 = new MemoryStream(newContent))
        {
            await _service.StoreFileAsync(stream2, fileName, "text/plain");
        }

        // Assert
        await using var result = await _service.GetFileAsync(fileName, CancellationToken.None);
        using var reader = new MemoryStream();
        await result.CopyToAsync(reader);
        reader.ToArray().Should().BeEquivalentTo(newContent);
    }

    [Fact]
    public async Task StoreFileAsync_Should_ThrowException_When_FileNameIsNull()
    {
        // Arrange
        using var stream = new MemoryStream("content"u8.ToArray());

        // Act
        var act = () => _service.StoreFileAsync(stream, null!, "text/plain");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task StoreFileAsync_Should_ThrowException_When_FileNameIsEmpty()
    {
        // Arrange
        using var stream = new MemoryStream("content"u8.ToArray());

        // Act
        var act = () => _service.StoreFileAsync(stream, string.Empty, "text/plain");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task StoreFileAsync_Should_ThrowException_When_PathTraversalAttempted()
    {
        // Arrange
        var maliciousFileName = "../../../etc/passwd";
        using var stream = new MemoryStream("malicious content"u8.ToArray());

        // Act
        var act = () => _service.StoreFileAsync(stream, maliciousFileName, "text/plain");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*path traversal*");
    }

    [Fact]
    public async Task StoreFileAsync_Should_ThrowException_When_TildePathAttempted()
    {
        // Arrange
        var maliciousFileName = "~/secret.txt";
        using var stream = new MemoryStream("content"u8.ToArray());

        // Act
        var act = () => _service.StoreFileAsync(stream, maliciousFileName, "text/plain");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*path traversal*");
    }

    [Fact]
    public async Task GetFileAsync_Should_ThrowException_When_PathTraversalAttempted()
    {
        // Arrange
        var maliciousFileName = "../../../etc/passwd";

        // Act
        var act = () => _service.GetFileAsync(maliciousFileName, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*path traversal*");
    }

    [Fact]
    public async Task GetFileAsync_Should_SupportCancellation()
    {
        // Arrange
        var fileName = "cancellation-test.txt";
        var content = "Test content"u8.ToArray();
        using var writeStream = new MemoryStream(content);
        await _service.StoreFileAsync(writeStream, fileName, "text/plain");

        var cts = new CancellationTokenSource();

        // Act
        await using var result = await _service.GetFileAsync(fileName, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFileAsync_Should_ThrowOperationCanceledException_When_Cancelled()
    {
        // Arrange
        var fileName = "cancelled-test.txt";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _service.GetFileAsync(fileName, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task StoreFileAsync_Should_HandleLargeFiles()
    {
        // Arrange
        var fileName = "large-file.bin";
        var content = new byte[1024 * 1024]; // 1MB
        Random.Shared.NextBytes(content);
        using var stream = new MemoryStream(content);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "application/octet-stream");

        // Assert
        var storedContent = await File.ReadAllBytesAsync(result);
        storedContent.Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task StoreFileAsync_Should_HandleEmptyStream()
    {
        // Arrange
        var fileName = "empty-file.txt";
        using var stream = new MemoryStream();

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "text/plain");

        // Assert
        var fileInfo = new FileInfo(result);
        fileInfo.Exists.Should().BeTrue();
        fileInfo.Length.Should().Be(0);
    }

    [Fact]
    public async Task GetFileAsync_Should_HandleAbsolutePathWithinStorage()
    {
        // Arrange
        var fileName = "absolute-test.txt";
        var content = "Test content"u8.ToArray();
        using var writeStream = new MemoryStream(content);
        var storedPath = await _service.StoreFileAsync(writeStream, fileName, "text/plain");

        // Act - Use the full path returned by store
        await using var result = await _service.GetFileAsync(storedPath, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        using var reader = new MemoryStream();
        await result.CopyToAsync(reader);
        reader.ToArray().Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task StoreFileAsync_Should_HandleSpecialCharactersInFileName()
    {
        // Arrange
        var fileName = "file with spaces (copy).txt";
        var content = "Content"u8.ToArray();
        using var stream = new MemoryStream(content);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "text/plain");

        // Assert
        File.Exists(result).Should().BeTrue();
    }

    [Fact]
    public async Task StoreFileAsync_Should_HandleUnicodeFileName()
    {
        // Arrange
        var fileName = "日本語ファイル.txt";
        var content = "Unicode content"u8.ToArray();
        using var stream = new MemoryStream(content);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "text/plain");

        // Assert
        File.Exists(result).Should().BeTrue();
    }

    [Fact]
    public async Task StoreFileAsync_Should_HandleWhitespaceOnlyFileName()
    {
        // Arrange
        using var stream = new MemoryStream("content"u8.ToArray());

        // Act
        var act = () => _service.StoreFileAsync(stream, "   ", "text/plain");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task StoreFileAsync_Should_StripNullBytes()
    {
        // Arrange
        var maliciousFileName = "file.txt\0.exe";
        using var stream = new MemoryStream("content"u8.ToArray());

        // Act
        var result = await _service.StoreFileAsync(stream, maliciousFileName, "text/plain");

        // Assert
        result.Should().NotContain("\0");
    }
}
