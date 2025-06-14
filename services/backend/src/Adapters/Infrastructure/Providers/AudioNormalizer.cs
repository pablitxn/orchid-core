using System.Diagnostics;
using Application.Interfaces;
using Domain.Events;
using MassTransit;

namespace Infrastructure.Providers;

/// <summary>
///     Implementation of IAudioNormalizer using FFmpeg.
/// </summary>
public class AudioNormalizer(IPublishEndpoint publishEndpoint) : IAudioNormalizer
{
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public async Task<byte[]> ConvertToMp3Async(byte[] inputAudioData)
    {
        // Use environment variable "FFMPEG_PATH" if defined, otherwise default to "ffmpeg"
        var ffmpegExecutable = Environment.GetEnvironmentVariable("FFMPEG_PATH") ?? "ffmpeg";

        // Save input audio data to a temporary file.
        var inputPath = Path.GetTempFileName();
        var outputPath = Path.ChangeExtension(inputPath, ".mp3");

        await File.WriteAllBytesAsync(inputPath, inputAudioData);

        // Use FFmpeg to convert the audio file to MP3 format at 128kbps.
        var ffmpegProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegExecutable,
                Arguments = $"-i \"{inputPath}\" -b:a 128k \"{outputPath}\" -y",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        ffmpegProcess.Start();
        await ffmpegProcess.WaitForExitAsync();

        // Read the converted file.
        var outputData = await File.ReadAllBytesAsync(outputPath);

        // Clean up temporary files.
        File.Delete(inputPath);
        File.Delete(outputPath);

        // Publish the AudioNormalizedEvent after successful conversion.
        await _publishEndpoint.Publish(new AudioNormalizedEvent(
            Guid.Empty,
            "projectName",
            DateTime.UtcNow,
            $"Project '{"projectName"}' normalized successfully at {DateTime.UtcNow:O}"
        ));

        return outputData;
    }
}