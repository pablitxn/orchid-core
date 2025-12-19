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
                UserId = Guid.Empty,
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
}