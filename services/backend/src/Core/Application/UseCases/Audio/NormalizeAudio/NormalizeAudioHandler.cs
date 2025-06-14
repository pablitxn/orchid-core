using Application.Interfaces;

namespace Application.UseCases.Audio.NormalizeAudio;

/// <summary>
///     Command handler for NormalizeAudioCommand.
/// </summary>
public class NormalizeAudioHandler(IAudioNormalizer audioNormalizer, IFileStorageService fileStorageService)
{
    private readonly NormalizeAudioCommandValidator _validator = new();

    /// <summary>
    ///     Handles the NormalizeAudioCommand by converting the audio to MP3 and uploading it.
    /// </summary>
    /// <param name="command">The command containing audio data and project information.</param>
    /// <returns>The URL of the uploaded normalized audio file.</returns>
    public async Task<string> HandleAsync(NormalizeAudioCommand command)
    {
        // Validate the command input.
        _validator.Validate(command);

        // Convert the audio data to MP3 at 128kbps.
        var normalizedAudio = await audioNormalizer.ConvertToMp3Async(command.AudioData);

        // Generate a file name for the normalized audio file.
        var normalizedFileName = $"{command.ProjectId}_normalized.mp3";

        // Generate stream for the normalized audio data.
        var normalizedStream = new MemoryStream(normalizedAudio);

        const string contentType = "audio/mpeg"; // MIME type for MP3

        // Upload the normalized file to cloud storage.
        var fileUrl = await fileStorageService.StoreFileAsync(normalizedStream, normalizedFileName, contentType);

        // Return the URL of the uploaded file.
        return fileUrl;
    }
}