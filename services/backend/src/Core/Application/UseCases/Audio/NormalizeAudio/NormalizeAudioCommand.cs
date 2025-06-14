namespace Application.UseCases.Audio.NormalizeAudio;

/// <summary>
///     Command to normalize an audio file to MP3 (128kbps) and upload it to cloud storage.
/// </summary>
public class NormalizeAudioCommand(Guid projectId, byte[] audioData, string originalFileName)
{
    public Guid ProjectId { get; } = projectId;
    public byte[] AudioData { get; } = audioData;
    public string OriginalFileName { get; } = originalFileName;
}