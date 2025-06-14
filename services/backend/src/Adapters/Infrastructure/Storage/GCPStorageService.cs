using Application.Interfaces;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Storage;

public class GCPStorageService(StorageClient client, string bucketName, ILogger<GCPStorageService> logger)
    : IFileStorageService
{
    private readonly string _bucket = bucketName;
    private readonly StorageClient _client = client;
    private readonly ILogger<GCPStorageService> _logger = logger;

    public async Task<string> StoreFileAsync(Stream normalizedStream, string fileName, string contentType)
    {
        try
        {
            var obj = await _client.UploadObjectAsync(_bucket, fileName, contentType, normalizedStream);
            // The fallback return value is a constructed URI string used when MediaLink is null.
            // Note: this fallback may behave differently than an actual MediaLink.
            return obj.MediaLink ?? $"gs://{_bucket}/{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} to bucket {Bucket}", fileName, _bucket);
            throw;
        }
    }

    public async Task<Stream> GetFileAsync(string fileName, CancellationToken cancellationToken)
    {
        // Verify that the file exists in the bucket
        var exists = _client
            .ListObjects(_bucket, fileName)
            .Any(obj => obj.Name == fileName);
        if (!exists)
        {
            var msg = $"File '{fileName}' not found in bucket '{_bucket}'.";
            _logger.LogError(msg);
            throw new FileNotFoundException(msg, fileName);
        }

        var ms = new MemoryStream();
        try
        {
            await _client.DownloadObjectAsync(_bucket, fileName, ms, cancellationToken: cancellationToken);
            ms.Position = 0;
            return ms;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {FileName} from bucket {Bucket}", fileName, _bucket);
            ms.Dispose();
            throw;
        }
    }

    public Task<Stream> GetFileAsync(string requestDocumentPath)
    {
        return GetFileAsync(requestDocumentPath, CancellationToken.None);
    }
}