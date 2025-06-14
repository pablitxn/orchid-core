using Application.Interfaces;

namespace Infrastructure.Providers;

public class LocalFileStorageService(string storagePath) : IFileStorageService
{
    private readonly string _storagePath = storagePath;

    public async Task<string> StoreFileAsync(Stream normalizedStream, string fileName, string contentType)
    {
        var filePath = Path.Combine(_storagePath, fileName);
        var dir = Path.GetDirectoryName(filePath) ?? _storagePath;
        Directory.CreateDirectory(dir);

        normalizedStream.Position = 0;
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await normalizedStream.CopyToAsync(fileStream);
        return filePath;
    }

    /// <inheritdoc />
    public async Task<Stream> GetFileAsync(string fileName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        // If the fileName is already an absolute path within our storage directory, use it directly
        string filePath;
        if (Path.IsPathRooted(fileName) && fileName.StartsWith(_storagePath))
        {
            filePath = fileName;
        }
        else
        {
            // Otherwise, treat it as a relative path and combine with storage path
            filePath = Path.Combine(_storagePath, fileName);
        }
        
        // If file doesn't exist at the expected path, try looking for just the filename in the storage tree
        if (!File.Exists(filePath) && !Path.IsPathRooted(fileName))
        {
            // Search for the file in the storage directory tree
            var searchPattern = Path.GetFileName(fileName);
            var foundFiles = Directory.GetFiles(_storagePath, searchPattern, SearchOption.AllDirectories);
            if (foundFiles.Length > 0)
            {
                filePath = foundFiles[0]; // Use the first match
            }
        }
        
        if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {fileName}", fileName);

        var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return await Task.FromResult(stream);
    }

    public Task<Stream> GetFileAsync(string requestDocumentPath)
    {
        return GetFileAsync(requestDocumentPath, CancellationToken.None);
    }
}