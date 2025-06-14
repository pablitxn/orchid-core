using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AudioProcessingWorker.Handlers;
using Domain.Events;
using Infrastructure.Providers;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using IBusControl = MassTransit.IBusControl;
using IPublishEndpoint = MassTransit.IPublishEndpoint;
using RabbitMqBusFactoryConfiguratorExtensions = MassTransit.RabbitMqBusFactoryConfiguratorExtensions;
using RabbitMqHostConfigurationExtensions = MassTransit.RabbitMqHostConfigurationExtensions;

namespace Workers.IntegrationTests.Messaging;

public class AudioNormalizerIntegrationTests : IAsyncLifetime
{
    private IBusControl _bus;
    private TaskCompletionSource<AudioNormalizedEvent> _eventReceivedTcs;
    private ServiceProvider _provider;

    public async Task InitializeAsync()
    {
        // Create a TaskCompletionSource to capture the event when it is received.
        _eventReceivedTcs = new TaskCompletionSource<AudioNormalizedEvent>();

        var services = new ServiceCollection();
        services.AddMassTransit(config =>
        {
            config.AddConsumer<ProjectCreatedHandler>();

            RabbitMqBusFactoryConfiguratorExtensions.UsingRabbitMq(config, (context, cfg) =>
            {
                RabbitMqHostConfigurationExtensions.Host(cfg, "localhost", "/", h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });
            });
        });

        services.AddMassTransitHostedService(true);

        _provider = services.BuildServiceProvider();
        _bus = _provider.GetRequiredService<IBusControl>();
        await _bus.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _bus.StopAsync();
        if (_provider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            _provider.Dispose();
    }

    [Fact]
    public async Task ConvertToMp3Async_ValidOggFile_ReturnsMp3Data_And_PublishesEvent()
    {
        // Arrange: Use the real publish endpoint from the bus.
        var publishEndpoint = _provider.GetRequiredService<IPublishEndpoint>();
        var audioNormalizer = new AudioNormalizer(publishEndpoint);

        // Build the path to the sample file in the TestResources folder.
        var sampleFilePath =
            Path.GetFullPath(Path.Combine("..", "..", "..", "TestResources", "audio_sample_1.ogg"));
        Assert.True(File.Exists(sampleFilePath), $"Sample file does not exist at path: {sampleFilePath}");

        var inputAudio = await File.ReadAllBytesAsync(sampleFilePath);

        // Act: Convert the audio file to MP3.
        var outputAudio = await audioNormalizer.ConvertToMp3Async(inputAudio);

        // Assert: Validate MP3 conversion.
        Assert.NotNull(outputAudio);
        Assert.True(outputAudio.Length > 1000, "The output MP3 file size is unexpectedly small.");

        // Optionally, check for MP3 header (e.g., the ID3 tag or MP3 frame header).
        var header = Encoding.ASCII.GetString(outputAudio, 0, 3);
        var hasId3 = header == "ID3";
        var hasMp3Frame = outputAudio[0] == 0xFF; // Simplistic check for an MP3 frame.
        Assert.True(hasId3 || hasMp3Frame, "Output does not appear to be a valid MP3 file.");

        // Wait for the AudioNormalizedEvent to be published and consumed.
        var completedTask = await Task.WhenAny(_eventReceivedTcs.Task, Task.Delay(15000));
        Assert.True(_eventReceivedTcs.Task.IsCompleted,
            "AudioNormalizedEvent was not received within the timeout.");

        var evt = _eventReceivedTcs.Task.Result;
        Assert.NotNull(evt);
        // Additional assertions can be added here depending on expected event properties.
        // For example: Assert.Equal(expectedProjectId, evt.ProjectId);
        // Assert.Contains("normalized successfully", evt.Message);

        // Log the received event.
        Console.WriteLine($"✅ Event received: {evt.Message}");
    }
}