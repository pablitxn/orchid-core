using System.Text;
using Infrastructure.Providers;

namespace Infrastructure.Tests.Providers;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly LocalFileStorageService _fileStorageService;
    private readonly string _tempFolder;

    public LocalFileStorageServiceTests()
    {
        // Create a temporary folder for testing
        _tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _fileStorageService = new LocalFileStorageService(_tempFolder);
    }

    public void Dispose()
    {
        // Cleanup temporary folder after test execution
        if (Directory.Exists(_tempFolder)) Directory.Delete(_tempFolder, true);
    }

    [Fact]
    public async Task StoreFileAsync_WritesFileToDisk()
    {
        // Arrange
        var fileName = "test.txt";
        var content = "This is a test file.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var filePath = await _fileStorageService.StoreFileAsync(stream, fileName, "text/plain");

        // Assert
        Assert.True(File.Exists(filePath));
        var storedContent = await File.ReadAllTextAsync(filePath);
        Assert.Equal(content, storedContent);
    }

    [Fact]
    public async Task GetFileAsync_WithAbsolutePath_ReturnsFile()
    {
        // Arrange
        var fileName = "test-absolute.txt";
        var content = "This is a test file for absolute path.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        
        // Store file and get absolute path
        var absolutePath = await _fileStorageService.StoreFileAsync(stream, fileName, "text/plain");

        // Act - retrieve using the absolute path
        using var retrievedStream = await _fileStorageService.GetFileAsync(absolutePath);
        using var reader = new StreamReader(retrievedStream);
        var retrievedContent = await reader.ReadToEndAsync();

        // Assert
        Assert.Equal(content, retrievedContent);
    }

    [Fact]
    public async Task GetFileAsync_WithRelativePath_ReturnsFile()
    {
        // Arrange
        var fileName = "test-relative.txt";
        var content = "This is a test file for relative path.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        
        // Store file
        await _fileStorageService.StoreFileAsync(stream, fileName, "text/plain");

        // Act - retrieve using just the filename (relative path)
        using var retrievedStream = await _fileStorageService.GetFileAsync(fileName);
        using var reader = new StreamReader(retrievedStream);
        var retrievedContent = await reader.ReadToEndAsync();

        // Assert
        Assert.Equal(content, retrievedContent);
    }

    [Fact]
    public async Task GetFileAsync_FileNotFound_ThrowsException()
    {
        // Arrange
        var nonExistentFile = "non-existent.txt";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _fileStorageService.GetFileAsync(nonExistentFile));
    }
}