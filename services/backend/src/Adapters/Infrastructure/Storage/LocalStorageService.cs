using Application.Interfaces;
using Infrastructure.Providers;

namespace Infrastructure.Storage;

public class LocalStorageService(string rootPath) : IFileStorageService
{
    private readonly LocalFileStorageService _inner = new(rootPath);

    public Task<string> StoreFileAsync(Stream normalizedStream, string fileName, string contentType)
    {
        return _inner.StoreFileAsync(normalizedStream, fileName, contentType);
    }

    public Task<Stream> GetFileAsync(string fileName, CancellationToken cancellationToken)
    {
        return _inner.GetFileAsync(fileName, cancellationToken);
    }

    public Task<Stream> GetFileAsync(string requestDocumentPath)
    {
        return _inner.GetFileAsync(requestDocumentPath);
    }
}