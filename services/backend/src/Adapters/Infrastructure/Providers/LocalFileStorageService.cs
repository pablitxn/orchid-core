using Application.Interfaces;

namespace Infrastructure.Providers;

public class LocalFileStorageService(string storagePath) : IFileStorageService
{
    private readonly string _storagePath = Path.GetFullPath(storagePath);

    /// <summary>
    /// Validates that the given path is within the storage directory to prevent path traversal attacks.
    /// </summary>
    /// <param name="fileName">The file name or relative path to validate.</param>
    /// <returns>The canonicalized full path if valid.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown if the path escapes the storage directory.</exception>
    private string ValidateAndResolvePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

        // Sanitize the filename - remove any null bytes and trim whitespace
        fileName = fileName.Replace("\0", "").Trim();

        // Block obvious path traversal attempts
        if (fileName.Contains("..") || fileName.Contains("~"))
            throw new UnauthorizedAccessException("Invalid file path: path traversal detected.");

        // Combine with storage path and get the canonical full path
        var candidatePath = Path.GetFullPath(Path.Combine(_storagePath, fileName));

        // Ensure the resolved path is within the storage directory
        // Use Path.DirectorySeparatorChar to ensure proper prefix matching
        var normalizedStoragePath = _storagePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                    + Path.DirectorySeparatorChar;

        if (!candidatePath.StartsWith(normalizedStoragePath, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Access denied: file path is outside the allowed storage directory.");

        return candidatePath;
    }

    public async Task<string> StoreFileAsync(Stream normalizedStream, string fileName, string contentType)
    {
        // Security: Validate and resolve the path to prevent path traversal attacks
        var filePath = ValidateAndResolvePath(fileName);
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

        // Security: Validate and resolve the path to prevent path traversal attacks
        // This handles both relative paths and validates that absolute paths are within storage
        string filePath;

        // If it looks like an absolute path, extract just the filename portion for safety
        if (Path.IsPathRooted(fileName))
        {
            // Only accept absolute paths that are already within our storage directory
            var fullPath = Path.GetFullPath(fileName);
            var normalizedStoragePath = _storagePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                        + Path.DirectorySeparatorChar;

            if (fullPath.StartsWith(normalizedStoragePath, StringComparison.OrdinalIgnoreCase))
            {
                filePath = fullPath;
            }
            else
            {
                // Reject absolute paths outside storage - use just the filename
                filePath = ValidateAndResolvePath(Path.GetFileName(fileName));
            }
        }
        else
        {
            filePath = ValidateAndResolvePath(fileName);
        }

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {Path.GetFileName(fileName)}", fileName);

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