using Application.Interfaces;
using Application.UseCases.Project.CreateProject;
using Application.UseCases.Project.GetProjectById;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectController(IMediator mediator, IFileStorageService fileStorageService) : ControllerBase
{
    private readonly IFileStorageService _fileStorageService = fileStorageService;
    private readonly IMediator _mediator = mediator;

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(
        [FromForm] string targetLanguage,
        [FromForm] IFormFile audioFile
    )
    {
        // Validate the request is multipart/form-data
        if (!Request.HasFormContentType) return BadRequest("Request content type must be multipart/form-data.");

        // Validate that the file is provided
        if (audioFile == null! || audioFile.Length == 0) return BadRequest("No file provided.");

        // Validate semantic data (e.g., targetLanguage)
        if (string.IsNullOrWhiteSpace(targetLanguage)) return BadRequest("Target language is required.");

        // Determine file extension and MIME type
        var fileExtension = Path.GetExtension(audioFile.FileName).ToLower();
        var contentType = audioFile.ContentType.ToLower();

        // Define allowed file types for audio
        var allowedAudioExtensions = new List<string> { ".mp3", ".wav", ".m4a" };
        var allowedAudioMimeTypes = new List<string> { "audio/mpeg", "audio/wav", "audio/mp3" };

        // Define allowed file types for documents
        var allowedDocumentExtensions = new List<string> { ".txt", ".doc", ".docx", ".pdf" };
        var allowedDocumentMimeTypes = new List<string>
        {
            "text/plain", "application/msword", "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };

        // Check if file is audio or document
        var isAudio = allowedAudioExtensions.Contains(fileExtension) && allowedAudioMimeTypes.Contains(contentType);
        var isDocument = allowedDocumentExtensions.Contains(fileExtension) &&
                         allowedDocumentMimeTypes.Contains(contentType);

        if (!isAudio && !isDocument) return BadRequest("The file extension or MIME type is not supported.");

        // Validate file size by type
        const long maxAudioFileSize = 50L * 1024L * 1024L;
        const long maxDocumentFileSize = 20L * 1024L * 1024L;

        if (isAudio && audioFile.Length > maxAudioFileSize)
            return BadRequest($"Audio file exceeds the maximum allowed size of {maxAudioFileSize / (1024 * 1024)}MB.");

        if (isDocument && audioFile.Length > maxDocumentFileSize)
            return BadRequest(
                $"Document file exceeds the maximum allowed size of {maxDocumentFileSize / (1024 * 1024)}MB.");

        // Generate a unique file name to avoid conflicts
        var fileName = $"{Guid.NewGuid()}_{audioFile.FileName}";

        try
        {
            await using var audioStream = audioFile.OpenReadStream();
            var url = await _fileStorageService.StoreFileAsync(audioStream, fileName, audioFile.ContentType);

            var command = new CreateProjectByAudioCommand(
                targetLanguage,
                fileName,
                contentType,
                audioFile.Length,
                url
            );

            var result = await _mediator.Send(command);

            return Ok(new
            {
                projectId = result.Id
            });
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while processing the file upload.");
        }
    }

    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> GetProject(Guid projectId)
    {
        var result = await _mediator.Send(new GetProjectByIdCommand(projectId));
        if (result == null!)
            return NotFound();
        return Ok(result);
    }
}