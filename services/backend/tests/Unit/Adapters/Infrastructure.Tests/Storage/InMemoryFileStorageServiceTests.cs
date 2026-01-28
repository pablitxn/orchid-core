using FluentAssertions;
using Infrastructure.Storage;

namespace Infrastructure.Tests.Storage;

public class InMemoryFileStorageServiceTests
{
    private readonly InMemoryFileStorageService _service;

    public InMemoryFileStorageServiceTests()
    {
        _service = new InMemoryFileStorageService();
    }

    [Fact]
    public async Task StoreFileAsync_Should_StoreFileInMemory()
    {
        // Arrange
        var fileName = "test-file.txt";
        var content = "Hello, World!"u8.ToArray();
        using var stream = new MemoryStream(content);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "text/plain");

        // Assert
        result.Should().Be(fileName);
    }

    [Fact]
    public async Task StoreFileAsync_Should_ReturnFileName()
    {
        // Arrange
        var fileName = "my-document.pdf";
        var content = "PDF content"u8.ToArray();
        using var stream = new MemoryStream(content);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "application/pdf");

        // Assert
        result.Should().Be(fileName);
    }

    [Fact]
    public async Task GetFileAsync_Should_ReturnStoredFile()
    {
        // Arrange
        var fileName = "retrieve-test.txt";
        var content = "Retrieve me!"u8.ToArray();
        using var writeStream = new MemoryStream(content);
        await _service.StoreFileAsync(writeStream, fileName, "text/plain");

        // Act
        var result = await _service.GetFileAsync(fileName, CancellationToken.None);

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
        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage($"*{fileName}*");
    }

    [Fact]
    public async Task GetFileAsync_Overload_Should_ReturnStoredFile()
    {
        // Arrange
        var fileName = "overload-test.txt";
        var content = "Overload content"u8.ToArray();
        using var writeStream = new MemoryStream(content);
        await _service.StoreFileAsync(writeStream, fileName, "text/plain");

        // Act
        var result = await _service.GetFileAsync(fileName);

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
        var result = await _service.GetFileAsync(fileName, CancellationToken.None);
        using var reader = new MemoryStream();
        await result.CopyToAsync(reader);
        reader.ToArray().Should().BeEquivalentTo(newContent);
    }

    [Fact]
    public async Task StoreFileAsync_Should_HandleMultipleFiles()
    {
        // Arrange
        var file1 = ("file1.txt", "Content 1"u8.ToArray());
        var file2 = ("file2.txt", "Content 2"u8.ToArray());
        var file3 = ("file3.txt", "Content 3"u8.ToArray());

        // Act
        using (var stream = new MemoryStream(file1.Item2))
            await _service.StoreFileAsync(stream, file1.Item1, "text/plain");
        using (var stream = new MemoryStream(file2.Item2))
            await _service.StoreFileAsync(stream, file2.Item1, "text/plain");
        using (var stream = new MemoryStream(file3.Item2))
            await _service.StoreFileAsync(stream, file3.Item1, "text/plain");

        // Assert
        var result1 = await _service.GetFileAsync(file1.Item1, CancellationToken.None);
        var result2 = await _service.GetFileAsync(file2.Item1, CancellationToken.None);
        var result3 = await _service.GetFileAsync(file3.Item1, CancellationToken.None);

        using var reader1 = new MemoryStream();
        using var reader2 = new MemoryStream();
        using var reader3 = new MemoryStream();

        await result1.CopyToAsync(reader1);
        await result2.CopyToAsync(reader2);
        await result3.CopyToAsync(reader3);

        reader1.ToArray().Should().BeEquivalentTo(file1.Item2);
        reader2.ToArray().Should().BeEquivalentTo(file2.Item2);
        reader3.ToArray().Should().BeEquivalentTo(file3.Item2);
    }

    [Fact]
    public async Task StoreFileAsync_Should_ResetStreamPosition()
    {
        // Arrange
        var fileName = "position-test.txt";
        var content = "Position test content"u8.ToArray();
        using var stream = new MemoryStream(content);

        // Move position to end
        stream.Position = stream.Length;

        // Act
        await _service.StoreFileAsync(stream, fileName, "text/plain");

        // Assert - File should have content (stream was reset)
        var result = await _service.GetFileAsync(fileName, CancellationToken.None);
        using var reader = new MemoryStream();
        await result.CopyToAsync(reader);
        reader.ToArray().Should().BeEquivalentTo(content);
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
        await _service.StoreFileAsync(stream, fileName, "application/octet-stream");

        // Assert
        var result = await _service.GetFileAsync(fileName, CancellationToken.None);
        using var reader = new MemoryStream();
        await result.CopyToAsync(reader);
        reader.ToArray().Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task StoreFileAsync_Should_HandleEmptyStream()
    {
        // Arrange
        var fileName = "empty-file.txt";
        using var stream = new MemoryStream();

        // Act
        await _service.StoreFileAsync(stream, fileName, "text/plain");

        // Assert
        var result = await _service.GetFileAsync(fileName, CancellationToken.None);
        result.Length.Should().Be(0);
    }

    [Fact]
    public async Task StoreFileAsync_Should_HandleBinaryContent()
    {
        // Arrange
        var fileName = "binary.bin";
        var content = new byte[] { 0x00, 0x01, 0xFF, 0xFE, 0x7F, 0x80 };
        using var stream = new MemoryStream(content);

        // Act
        await _service.StoreFileAsync(stream, fileName, "application/octet-stream");

        // Assert
        var result = await _service.GetFileAsync(fileName, CancellationToken.None);
        using var reader = new MemoryStream();
        await result.CopyToAsync(reader);
        reader.ToArray().Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task GetFileAsync_Should_ReturnIndependentStreams()
    {
        // Arrange
        var fileName = "independent-stream.txt";
        var content = "Stream independence test"u8.ToArray();
        using var writeStream = new MemoryStream(content);
        await _service.StoreFileAsync(writeStream, fileName, "text/plain");

        // Act
        var stream1 = await _service.GetFileAsync(fileName, CancellationToken.None);
        var stream2 = await _service.GetFileAsync(fileName, CancellationToken.None);

        // Assert - Reading from one shouldn't affect the other
        var buffer1 = new byte[5];
        await stream1.ReadAsync(buffer1);

        var buffer2 = new byte[content.Length];
        var bytesRead = await stream2.ReadAsync(buffer2);

        bytesRead.Should().Be(content.Length);
        buffer2.Should().BeEquivalentTo(content);
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
        result.Should().Be(fileName);
        var retrieved = await _service.GetFileAsync(fileName, CancellationToken.None);
        retrieved.Should().NotBeNull();
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
        result.Should().Be(fileName);
        var retrieved = await _service.GetFileAsync(fileName, CancellationToken.None);
        retrieved.Should().NotBeNull();
    }

    [Fact]
    public async Task StoreFileAsync_Should_HandlePathLikeFileNames()
    {
        // Arrange
        var fileName = "folder/subfolder/file.txt";
        var content = "Path content"u8.ToArray();
        using var stream = new MemoryStream(content);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "text/plain");

        // Assert
        result.Should().Be(fileName);
        var retrieved = await _service.GetFileAsync(fileName, CancellationToken.None);
        using var reader = new MemoryStream();
        await retrieved.CopyToAsync(reader);
        reader.ToArray().Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task Service_Should_BeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var fileCount = 100;

        // Act
        for (var i = 0; i < fileCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var fileName = $"concurrent-file-{index}.txt";
                var content = System.Text.Encoding.UTF8.GetBytes($"Content {index}");
                using var stream = new MemoryStream(content);
                await _service.StoreFileAsync(stream, fileName, "text/plain");
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All files should be retrievable
        for (var i = 0; i < fileCount; i++)
        {
            var fileName = $"concurrent-file-{i}.txt";
            var result = await _service.GetFileAsync(fileName, CancellationToken.None);
            result.Should().NotBeNull();
        }
    }
}
