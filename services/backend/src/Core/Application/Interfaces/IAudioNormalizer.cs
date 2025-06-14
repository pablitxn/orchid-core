namespace Application.Interfaces;

public interface IAudioNormalizer
{
    /// <summary>
    ///     Normalizes the audio file stream and returns a normalized version.
    ///     Port for converting audio to MP3 (128kbps).
    /// </summary>
    /// <param name="inputAudioData">Input audio data as a byte array</param>
    /// <returns>Normalized audio file as a stream</returns>
    Task<byte[]> ConvertToMp3Async(byte[] inputAudioData);
}