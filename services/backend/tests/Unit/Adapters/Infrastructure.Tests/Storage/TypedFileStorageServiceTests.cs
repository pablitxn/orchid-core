using Application.Interfaces;
using FluentAssertions;
using Infrastructure.Storage;
using Moq;

namespace Infrastructure.Tests.Storage;

public class TypedFileStorageServiceTests
{
    private readonly Mock<IFileStorageService> _innerStorageMock;
    private readonly TypedFileStorageService _service;
    private readonly string _rootFolder = "uploads";

    public TypedFileStorageServiceTests()
    {
        _innerStorageMock = new Mock<IFileStorageService>();
        _service = new TypedFileStorageService(_innerStorageMock.Object, _rootFolder);
    }

    [Theory]
    [InlineData("audio.mp3", "audio")]
    [InlineData("audio.wav", "audio")]
    [InlineData("audio.m4a", "audio")]
    public async Task StoreFileAsync_Should_ClassifyAudioFiles(string fileName, string expectedFolder)
    {
        // Arrange
        var content = "audio content"u8.ToArray();
        using var stream = new MemoryStream(content);
        var expectedPath = Path.Combine(_rootFolder, expectedFolder, fileName);

        _innerStorageMock
            .Setup(x => x.StoreFileAsync(It.IsAny<Stream>(), expectedPath, It.IsAny<string>()))
            .ReturnsAsync(expectedPath);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "audio/mpeg");

        // Assert
        result.Should().Be(expectedPath);
        _innerStorageMock.Verify(x => x.StoreFileAsync(stream, expectedPath, "audio/mpeg"), Times.Once);
    }

    [Theory]
    [InlineData("video.mp4", "video")]
    [InlineData("video.mov", "video")]
    [InlineData("video.avi", "video")]
    public async Task StoreFileAsync_Should_ClassifyVideoFiles(string fileName, string expectedFolder)
    {
        // Arrange
        var content = "video content"u8.ToArray();
        using var stream = new MemoryStream(content);
        var expectedPath = Path.Combine(_rootFolder, expectedFolder, fileName);

        _innerStorageMock
            .Setup(x => x.StoreFileAsync(It.IsAny<Stream>(), expectedPath, It.IsAny<string>()))
            .ReturnsAsync(expectedPath);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "video/mp4");

        // Assert
        result.Should().Be(expectedPath);
    }

    [Theory]
    [InlineData("image.png", "images")]
    [InlineData("image.jpeg", "images")]
    [InlineData("image.jpg", "images")]
    [InlineData("image.webp", "images")]
    public async Task StoreFileAsync_Should_ClassifyImageFiles(string fileName, string expectedFolder)
    {
        // Arrange
        var content = "image content"u8.ToArray();
        using var stream = new MemoryStream(content);
        var expectedPath = Path.Combine(_rootFolder, expectedFolder, fileName);

        _innerStorageMock
            .Setup(x => x.StoreFileAsync(It.IsAny<Stream>(), expectedPath, It.IsAny<string>()))
            .ReturnsAsync(expectedPath);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "image/png");

        // Assert
        result.Should().Be(expectedPath);
    }

    [Theory]
    [InlineData("document.pdf", "documents")]
    [InlineData("document.xls", "documents")]
    [InlineData("document.xlsx", "documents")]
    [InlineData("document.doc", "documents")]
    [InlineData("document.docx", "documents")]
    [InlineData("document.txt", "documents")]
    public async Task StoreFileAsync_Should_ClassifyDocumentFiles(string fileName, string expectedFolder)
    {
        // Arrange
        var content = "document content"u8.ToArray();
        using var stream = new MemoryStream(content);
        var expectedPath = Path.Combine(_rootFolder, expectedFolder, fileName);

        _innerStorageMock
            .Setup(x => x.StoreFileAsync(It.IsAny<Stream>(), expectedPath, It.IsAny<string>()))
            .ReturnsAsync(expectedPath);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "application/pdf");

        // Assert
        result.Should().Be(expectedPath);
    }

    [Fact]
    public async Task StoreFileAsync_Should_ClassifyGifFiles()
    {
        // Arrange
        var fileName = "animation.gif";
        var content = "gif content"u8.ToArray();
        using var stream = new MemoryStream(content);
        var expectedPath = Path.Combine(_rootFolder, "gifs", fileName);

        _innerStorageMock
            .Setup(x => x.StoreFileAsync(It.IsAny<Stream>(), expectedPath, It.IsAny<string>()))
            .ReturnsAsync(expectedPath);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "image/gif");

        // Assert
        result.Should().Be(expectedPath);
    }

    [Fact]
    public async Task StoreFileAsync_Should_ThrowException_ForUnsupportedFileType()
    {
        // Arrange
        var fileName = "file.xyz";
        var content = "unknown content"u8.ToArray();
        using var stream = new MemoryStream(content);

        // Act
        var act = () => _service.StoreFileAsync(stream, fileName, "application/octet-stream");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unsupported file type*");
    }

    [Fact]
    public async Task GetFileAsync_Should_TryDirectPathFirst()
    {
        // Arrange
        var fileName = "test-file.txt";
        var content = "test content"u8.ToArray();
        var mockStream = new MemoryStream(content);

        _innerStorageMock
            .Setup(x => x.GetFileAsync(fileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        // Act
        var result = await _service.GetFileAsync(fileName, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(mockStream);
        _innerStorageMock.Verify(x => x.GetFileAsync(fileName, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("audio.mp3", "audio")]
    [InlineData("audio.wav", "audio")]
    [InlineData("audio.m4a", "audio")]
    public async Task GetFileAsync_Should_FallbackToTypedPath_ForAudioFiles(string fileName, string folder)
    {
        // Arrange
        var content = "audio content"u8.ToArray();
        var mockStream = new MemoryStream(content);
        var expectedPath = Path.Combine(_rootFolder, folder, fileName);

        _innerStorageMock
            .Setup(x => x.GetFileAsync(fileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());

        _innerStorageMock
            .Setup(x => x.GetFileAsync(expectedPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        // Act
        var result = await _service.GetFileAsync(fileName, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(mockStream);
    }

    [Theory]
    [InlineData("video.mp4", "video")]
    [InlineData("video.mov", "video")]
    [InlineData("video.avi", "video")]
    public async Task GetFileAsync_Should_FallbackToTypedPath_ForVideoFiles(string fileName, string folder)
    {
        // Arrange
        var content = "video content"u8.ToArray();
        var mockStream = new MemoryStream(content);
        var expectedPath = Path.Combine(_rootFolder, folder, fileName);

        _innerStorageMock
            .Setup(x => x.GetFileAsync(fileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());

        _innerStorageMock
            .Setup(x => x.GetFileAsync(expectedPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        // Act
        var result = await _service.GetFileAsync(fileName, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(mockStream);
    }

    [Theory]
    [InlineData("image.png", "images")]
    [InlineData("image.jpeg", "images")]
    [InlineData("image.jpg", "images")]
    [InlineData("image.webp", "images")]
    public async Task GetFileAsync_Should_FallbackToTypedPath_ForImageFiles(string fileName, string folder)
    {
        // Arrange
        var content = "image content"u8.ToArray();
        var mockStream = new MemoryStream(content);
        var expectedPath = Path.Combine(_rootFolder, folder, fileName);

        _innerStorageMock
            .Setup(x => x.GetFileAsync(fileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());

        _innerStorageMock
            .Setup(x => x.GetFileAsync(expectedPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        // Act
        var result = await _service.GetFileAsync(fileName, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(mockStream);
    }

    [Theory]
    [InlineData("document.pdf", "documents")]
    [InlineData("document.doc", "documents")]
    [InlineData("document.txt", "documents")]
    public async Task GetFileAsync_Should_FallbackToTypedPath_ForDocumentFiles(string fileName, string folder)
    {
        // Arrange
        var content = "document content"u8.ToArray();
        var mockStream = new MemoryStream(content);
        var expectedPath = Path.Combine(_rootFolder, folder, fileName);

        _innerStorageMock
            .Setup(x => x.GetFileAsync(fileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());

        _innerStorageMock
            .Setup(x => x.GetFileAsync(expectedPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        // Act
        var result = await _service.GetFileAsync(fileName, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(mockStream);
    }

    [Fact]
    public async Task GetFileAsync_Should_FallbackToTypedPath_ForGifFiles()
    {
        // Arrange
        var fileName = "animation.gif";
        var content = "gif content"u8.ToArray();
        var mockStream = new MemoryStream(content);
        var expectedPath = Path.Combine(_rootFolder, "gifs", fileName);

        _innerStorageMock
            .Setup(x => x.GetFileAsync(fileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());

        _innerStorageMock
            .Setup(x => x.GetFileAsync(expectedPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        // Act
        var result = await _service.GetFileAsync(fileName, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(mockStream);
    }

    [Fact]
    public async Task GetFileAsync_Should_Rethrow_ForUnknownFileType()
    {
        // Arrange
        var fileName = "file.xyz";

        _innerStorageMock
            .Setup(x => x.GetFileAsync(fileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());

        // Act
        var act = () => _service.GetFileAsync(fileName, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task GetFileAsync_Overload_Should_CallMainMethod()
    {
        // Arrange
        var fileName = "test.txt";
        var content = "content"u8.ToArray();
        var mockStream = new MemoryStream(content);

        _innerStorageMock
            .Setup(x => x.GetFileAsync(fileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        // Act
        var result = await _service.GetFileAsync(fileName);

        // Assert
        result.Should().BeSameAs(mockStream);
    }

    [Fact]
    public void Constructor_Should_TrimTrailingSlashes()
    {
        // Arrange
        var rootWithSlash = "uploads/";
        var innerMock = new Mock<IFileStorageService>();
        var service = new TypedFileStorageService(innerMock.Object, rootWithSlash);

        // Act & Assert - The path should not have double slashes
        var fileName = "test.mp3";
        var expectedPath = Path.Combine("uploads", "audio", fileName);

        innerMock
            .Setup(x => x.StoreFileAsync(It.IsAny<Stream>(), expectedPath, It.IsAny<string>()))
            .ReturnsAsync(expectedPath);

        using var stream = new MemoryStream("content"u8.ToArray());
        var task = service.StoreFileAsync(stream, fileName, "audio/mpeg");

        innerMock.Verify(x => x.StoreFileAsync(It.IsAny<Stream>(), expectedPath, "audio/mpeg"), Times.Once);
    }

    [Fact]
    public void Constructor_Should_TrimTrailingBackslashes()
    {
        // Arrange
        var rootWithBackslash = "uploads\\";
        var innerMock = new Mock<IFileStorageService>();
        var service = new TypedFileStorageService(innerMock.Object, rootWithBackslash);

        // Act & Assert - The path should not have double separators
        var fileName = "test.mp3";
        var expectedPath = Path.Combine("uploads", "audio", fileName);

        innerMock
            .Setup(x => x.StoreFileAsync(It.IsAny<Stream>(), expectedPath, It.IsAny<string>()))
            .ReturnsAsync(expectedPath);

        using var stream = new MemoryStream("content"u8.ToArray());
        var task = service.StoreFileAsync(stream, fileName, "audio/mpeg");

        innerMock.Verify(x => x.StoreFileAsync(It.IsAny<Stream>(), expectedPath, "audio/mpeg"), Times.Once);
    }

    [Fact]
    public async Task StoreFileAsync_Should_HandleUppercaseExtensions()
    {
        // Arrange
        var fileName = "IMAGE.PNG";
        var content = "image content"u8.ToArray();
        using var stream = new MemoryStream(content);
        var expectedPath = Path.Combine(_rootFolder, "images", fileName);

        _innerStorageMock
            .Setup(x => x.StoreFileAsync(It.IsAny<Stream>(), expectedPath, It.IsAny<string>()))
            .ReturnsAsync(expectedPath);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "image/png");

        // Assert
        result.Should().Be(expectedPath);
    }

    [Fact]
    public async Task StoreFileAsync_Should_HandleMixedCaseExtensions()
    {
        // Arrange
        var fileName = "document.PdF";
        var content = "pdf content"u8.ToArray();
        using var stream = new MemoryStream(content);
        var expectedPath = Path.Combine(_rootFolder, "documents", fileName);

        _innerStorageMock
            .Setup(x => x.StoreFileAsync(It.IsAny<Stream>(), expectedPath, It.IsAny<string>()))
            .ReturnsAsync(expectedPath);

        // Act
        var result = await _service.StoreFileAsync(stream, fileName, "application/pdf");

        // Assert
        result.Should().Be(expectedPath);
    }

    [Fact]
    public async Task GetFileAsync_Should_SupportCancellation()
    {
        // Arrange
        var fileName = "test.txt";
        var cts = new CancellationTokenSource();
        var mockStream = new MemoryStream("content"u8.ToArray());

        _innerStorageMock
            .Setup(x => x.GetFileAsync(fileName, cts.Token))
            .ReturnsAsync(mockStream);

        // Act
        var result = await _service.GetFileAsync(fileName, cts.Token);

        // Assert
        _innerStorageMock.Verify(x => x.GetFileAsync(fileName, cts.Token), Times.Once);
    }
}
