namespace Application.UseCases.Audio.NormalizeAudio;

/// <summary>
///     Validator for NormalizeAudioCommand.
/// </summary>
public class NormalizeAudioCommandValidator
{
    public void Validate(NormalizeAudioCommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        if (command.ProjectId == Guid.Empty)
            throw new ArgumentException("ProjectId cannot be empty.", nameof(command.ProjectId));

        if (command.AudioData == null || command.AudioData.Length == 0)
            throw new ArgumentException("AudioData must be provided.", nameof(command.AudioData));

        if (string.IsNullOrWhiteSpace(command.OriginalFileName))
            throw new ArgumentException("OriginalFileName must be provided.", nameof(command.OriginalFileName));
    }
}