namespace Application.Interfaces;

public interface IFileStorageService
{
    /// <summary>
    ///     Stores the audio stream in the underlying storage (local, S3, etc.)
    /// </summary>
    /// <param name="normalizedStream">The normalized audio data</param>
    /// <param name="fileName">File name to be used for storage</param>
    /// <param name="contentType">MIME type of the file</param>
    /// <returns>URL or path to the stored file</returns>
    Task<string> StoreFileAsync(Stream normalizedStream, string fileName, string contentType);

    /// <summary>
    ///     Retrieves a stored file as a read-only stream.
    /// </summary>
    /// <param name="fileName">The name of the file to retrieve.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>Read-only stream of the file content.</returns>
    Task<Stream> GetFileAsync(string fileName, CancellationToken cancellationToken);

    Task<Stream> GetFileAsync(string requestDocumentPath);
}