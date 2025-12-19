using System.Security.Claims;
using Application.Interfaces;
using Core.Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/files")]
public class FilesController(
    IFileStorageService fileStorageService,
    IActivityPublisher activityPublisher,
    IDocumentRepository documentRepository,
    IKnowledgeBaseFileRepository kbRepository,
    IMediaCenterAssetRepository assetRepository) : ControllerBase
{
    // Security: 100 MB limit as per architecture guidelines
    private const long MaxFileSizeBytes = 100 * 1024 * 1024;

    // Security: Whitelist of allowed content types
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Documents
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "text/plain",
        "text/csv",
        "text/markdown",
        "application/json",
        "application/xml",
        "text/xml",
        // Images
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/svg+xml",
        "image/bmp",
        "image/tiff",
        // Audio
        "audio/mpeg",
        "audio/wav",
        "audio/ogg",
        "audio/webm",
        "audio/mp4",
        // Video
        "video/mp4",
        "video/webm",
        "video/ogg",
        "video/quicktime",
        // Archives (for data import)
        "application/zip",
        "application/x-zip-compressed"
    };

    // Security: Magic bytes (file signatures) for content type validation
    // This prevents content type spoofing attacks
    private static readonly Dictionary<string, byte[][]> MagicBytes = new()
    {
        // Images
        { "image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } },
        { "image/png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
        { "image/gif", new[] { new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 } } },
        { "image/webp", new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } } }, // RIFF (WebP starts with RIFF)
        { "image/bmp", new[] { new byte[] { 0x42, 0x4D } } },
        { "image/tiff", new[] { new byte[] { 0x49, 0x49, 0x2A, 0x00 }, new byte[] { 0x4D, 0x4D, 0x00, 0x2A } } },
        // Documents
        { "application/pdf", new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } } }, // %PDF
        { "application/zip", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, new byte[] { 0x50, 0x4B, 0x05, 0x06 }, new byte[] { 0x50, 0x4B, 0x07, 0x08 } } },
        { "application/x-zip-compressed", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } } },
        // Office documents (OOXML - ZIP based)
        { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } } },
        { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } } },
        { "application/vnd.openxmlformats-officedocument.presentationml.presentation", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } } },
        // Legacy Office documents (OLE Compound)
        { "application/msword", new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } } },
        { "application/vnd.ms-excel", new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } } },
        { "application/vnd.ms-powerpoint", new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } } },
        // Audio
        { "audio/mpeg", new[] { new byte[] { 0xFF, 0xFB }, new byte[] { 0xFF, 0xFA }, new byte[] { 0xFF, 0xF3 }, new byte[] { 0xFF, 0xF2 }, new byte[] { 0x49, 0x44, 0x33 } } }, // MP3 or ID3
        { "audio/wav", new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } } }, // RIFF
        { "audio/ogg", new[] { new byte[] { 0x4F, 0x67, 0x67, 0x53 } } }, // OggS
        // Video
        { "video/mp4", new[] { new byte[] { 0x00, 0x00, 0x00 } } }, // ftyp marker (varies)
        { "video/webm", new[] { new byte[] { 0x1A, 0x45, 0xDF, 0xA3 } } }, // EBML
        { "video/ogg", new[] { new byte[] { 0x4F, 0x67, 0x67, 0x53 } } },
        { "video/quicktime", new[] { new byte[] { 0x00, 0x00, 0x00 } } } // ftyp marker
    };

    /// <summary>
    /// Validates that file content matches the declared content type by checking magic bytes.
    /// </summary>
    private static bool ValidateMagicBytes(Stream stream, string contentType)
    {
        if (!MagicBytes.TryGetValue(contentType, out var signatures))
        {
            // For text-based types (plain text, csv, json, xml, markdown, svg), skip magic byte validation
            // as they don't have reliable signatures
            var textTypes = new[]
            {
                "text/plain", "text/csv", "text/markdown", "application/json",
                "application/xml", "text/xml", "image/svg+xml", "audio/webm", "audio/mp4"
            };
            return textTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);
        }

        // Read enough bytes to check the longest signature
        var maxLength = signatures.Max(s => s.Length);
        var buffer = new byte[maxLength];
        var originalPosition = stream.Position;

        try
        {
            stream.Position = 0;
            var bytesRead = stream.Read(buffer, 0, maxLength);

            // Check if any signature matches
            foreach (var signature in signatures)
            {
                if (bytesRead >= signature.Length)
                {
                    var matches = true;
                    for (var i = 0; i < signature.Length; i++)
                    {
                        if (buffer[i] != signature[i])
                        {
                            matches = false;
                            break;
                        }
                    }
                    if (matches) return true;
                }
            }

            return false;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    /// <summary>
    ///     Uploads a file and returns its URL and metadata.
    /// </summary>
    /// <param name="file">The file sent as multipart/form-data.</param>
    /// <param name="sessionId">The session ID to associate the file with.</param>
    /// <param name="type">
    ///     Optional document type. Valid values are
    ///     "attachment" or "knowledge". Defaults to attachment when omitted.
    ///     Invalid values result in a bad request.
    /// </param>
    /// <returns>Metadata about the stored file.</returns>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, [FromForm] string? sessionId,
        [FromForm] string? type)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("SessionId is required.");
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

        // Security: Validate file size
        if (file.Length > MaxFileSizeBytes)
            return BadRequest($"File size exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");

        // Security: Validate content type
        if (string.IsNullOrEmpty(file.ContentType) || !AllowedContentTypes.Contains(file.ContentType))
            return BadRequest($"File type '{file.ContentType}' is not allowed. Allowed types: documents, images, audio, video.");

        // Security: Validate magic bytes to prevent content type spoofing
        await using var validationStream = file.OpenReadStream();
        if (!ValidateMagicBytes(validationStream, file.ContentType))
            return BadRequest("File content does not match the declared content type.");

        var docType = DocumentEnum.Attachment;
        if (!string.IsNullOrWhiteSpace(type))
            if (!Enum.TryParse(type, true, out docType))
                return BadRequest("Invalid document type.");

        // Sanitize the file name
        var fileName =
            $"{Guid.NewGuid()}_{Path.GetFileNameWithoutExtension(file.FileName)}{Path.GetExtension(file.FileName)}";

        // Notify clients that the file upload request was received and store the activity in history
        await activityPublisher.PublishAsync("file_received", new { fileName });
        // Store the file
        await using var stream = file.OpenReadStream();
        var storedPath = await fileStorageService.StoreFileAsync(stream, fileName, file.ContentType);

        // Notify clients that the file was saved successfully
        await activityPublisher.PublishAsync("file_saved", new { fileName, storedPath });

        // Persist document metadata in database
        // Extract file ID from fileName prefix
        Guid fileId;
        var namePart = Path.GetFileNameWithoutExtension(fileName);
        var parts = namePart.Split('_', 2);
        if (!Guid.TryParse(parts[0], out fileId)) fileId = Guid.NewGuid();
        var document = new DocumentEntity
        {
            Id = fileId,
            SessionId = sessionId,
            DocumentPath = storedPath,
            FileName = fileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            Enum = docType
        };
        await documentRepository.AddAsync(document);

        if (docType == DocumentEnum.KnowledgeBase)
        {
            var kb = new KnowledgeBaseFileEntity
            {
                Id = Guid.NewGuid(),
                UserId = GetUserId(),
                Title = fileName,
                MimeType = file.ContentType,
                FileUrl = storedPath
            };
            await kbRepository.AddAsync(kb);

            if (file.ContentType.StartsWith("audio") || file.ContentType.StartsWith("image") ||
                file.ContentType.StartsWith("video"))
            {
                var asset = new MediaCenterAssetEntity
                {
                    Id = Guid.NewGuid(),
                    KnowledgeBaseFileId = kb.Id,
                    MimeType = file.ContentType,
                    Title = fileName,
                    FileUrl = storedPath,
                    CreatedAt = DateTime.UtcNow
                };
                await assetRepository.AddAsync(asset);
            }
        }

        return Ok(new
        {
            fileName,
            storedPath,
            contentType = file.ContentType,
            size = file.Length
        });
    }

    /// <summary>
    /// Extracts the authenticated user's ID from JWT claims.
    /// </summary>
    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID claim not found or invalid in token.");
        }

        return userId;
    }
}