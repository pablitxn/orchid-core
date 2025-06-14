using Application.Interfaces;

namespace Infrastructure.Storage;

/// <summary>
///     Wraps another <see cref="IFileStorageService" /> and stores files under
///     type-based directories.
/// </summary>
public class TypedFileStorageService(IFileStorageService inner, string rootFolder) : IFileStorageService
{
    private readonly IFileStorageService _inner = inner;
    private readonly string _root = rootFolder.TrimEnd('/', '\\');

    public async Task<Stream> GetFileAsync(string fileName, CancellationToken ct)
    {
        // First try with the fileName as-is (in case it already includes the path)
        try
        {
            return await _inner.GetFileAsync(fileName, ct);
        }
        catch (FileNotFoundException)
        {
            // If not found, try to determine the folder based on file extension
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var folder = ext switch
            {
                ".mp3" or ".wav" or ".m4a" => "audio",
                ".mp4" or ".mov" or ".avi" => "video",
                ".png" or ".jpeg" or ".jpg" or ".webp" => "images",
                ".pdf" or ".xls" or ".xlsx" or ".doc" or ".docx" or ".txt" => "documents",
                ".gif" => "gifs",
                _ => null
            };

            if (folder != null)
            {
                var pathWithFolder = Path.Combine(_root, folder, fileName);
                return await _inner.GetFileAsync(pathWithFolder, ct);
            }
            
            throw;
        }
    }

    public Task<Stream> GetFileAsync(string requestDocumentPath)
    {
        return GetFileAsync(requestDocumentPath, CancellationToken.None);
    }

    public async Task<string> StoreFileAsync(Stream stream, string fileName, string contentType)
    {
        var folder = Classify(contentType, fileName);
        var path = Path.Combine(_root, folder, fileName);
        return await _inner.StoreFileAsync(stream, path, contentType);
    }

    private static string Classify(string mimeType, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".mp3" or ".wav" or ".m4a" => "audio",
            ".mp4" or ".mov" or ".avi" => "video",
            ".png" or ".jpeg" or ".jpg" or ".webp" => "images",
            ".pdf" or ".xls" or ".xlsx" or ".doc" or ".docx" or ".txt" => "documents",
            ".gif" => "gifs",
            _ => throw new InvalidOperationException($"Unsupported file type {ext}")
        };
    }
}