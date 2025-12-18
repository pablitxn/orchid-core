using System.Diagnostics;
using Application.Interfaces;
using Domain.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

/// <summary>
///     Implementation of IAudioNormalizer using FFmpeg with stdin/stdout pipes.
///     This implementation avoids writing to the filesystem, making it suitable for cloud deployment.
/// </summary>
public class AudioNormalizer(IPublishEndpoint publishEndpoint, ILogger<AudioNormalizer> logger) : IAudioNormalizer
{
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ILogger<AudioNormalizer> _logger = logger;

    public async Task<byte[]> ConvertToMp3Async(byte[] inputAudioData)
    {
        // Use environment variable "FFMPEG_PATH" if defined, otherwise default to "ffmpeg"
        var ffmpegExecutable = Environment.GetEnvironmentVariable("FFMPEG_PATH") ?? "ffmpeg";

        // Use FFmpeg with stdin/stdout pipes instead of temporary files.
        // -i pipe:0 reads from stdin, -f mp3 pipe:1 writes to stdout
        var ffmpegProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegExecutable,
                Arguments = "-i pipe:0 -b:a 128k -f mp3 pipe:1",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            ffmpegProcess.Start();

            // Write input data to stdin in a separate task
            var writeTask = Task.Run(async () =>
            {
                await ffmpegProcess.StandardInput.BaseStream.WriteAsync(inputAudioData);
                ffmpegProcess.StandardInput.Close();
            });

            // Read output data from stdout
            using var outputStream = new MemoryStream();
            var readTask = ffmpegProcess.StandardOutput.BaseStream.CopyToAsync(outputStream);

            // Wait for both operations
            await Task.WhenAll(writeTask, readTask);
            await ffmpegProcess.WaitForExitAsync();

            if (ffmpegProcess.ExitCode != 0)
            {
                var error = await ffmpegProcess.StandardError.ReadToEndAsync();
                _logger.LogError("FFmpeg failed with exit code {ExitCode}: {Error}", ffmpegProcess.ExitCode, error);
                throw new InvalidOperationException($"FFmpeg conversion failed: {error}");
            }

            var outputData = outputStream.ToArray();

            // Publish the AudioNormalizedEvent after successful conversion.
            await _publishEndpoint.Publish(new AudioNormalizedEvent(
                Guid.Empty,
                "projectName",
                DateTime.UtcNow,
                $"Audio normalized successfully at {DateTime.UtcNow:O}"
            ));

            return outputData;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to convert audio using FFmpeg. Ensure FFmpeg is installed.");
            throw new InvalidOperationException("Audio conversion failed. FFmpeg may not be available.", ex);
        }
        finally
        {
            ffmpegProcess.Dispose();
        }
    }
}