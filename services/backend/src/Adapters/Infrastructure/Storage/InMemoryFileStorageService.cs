using System.Collections.Concurrent;
using Application.Interfaces;

namespace Infrastructure.Storage;

public class InMemoryFileStorageService : IFileStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    public async Task<string> StoreFileAsync(Stream normalizedStream, string fileName, string contentType)
    {
        using var ms = new MemoryStream();
        normalizedStream.Position = 0;
        await normalizedStream.CopyToAsync(ms);
        _files[fileName] = ms.ToArray();
        return fileName;
    }

    public Task<Stream> GetFileAsync(string fileName, CancellationToken cancellationToken)
    {
        if (!_files.TryGetValue(fileName, out var bytes))
            throw new FileNotFoundException($"File not found: {fileName}", fileName);
        Stream s = new MemoryStream(bytes);
        return Task.FromResult(s);
    }

    public Task<Stream> GetFileAsync(string requestDocumentPath)
    {
        return GetFileAsync(requestDocumentPath, CancellationToken.None);
    }
}