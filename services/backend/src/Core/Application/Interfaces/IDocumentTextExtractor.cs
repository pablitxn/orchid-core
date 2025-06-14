namespace Application.Interfaces;

/// <summary>
///     Extracts plain text from various document formats.
/// </summary>
public interface IDocumentTextExtractor
{
    /// <summary>
    ///     Reads the stream and returns plain text using the file extension for format detection.
    /// </summary>
    /// <param name="stream">File contents to parse.</param>
    /// <param name="extension">File extension starting with a dot (e.g. ".txt").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string> ExtractAsync(Stream stream, string extension, CancellationToken cancellationToken = default);
}