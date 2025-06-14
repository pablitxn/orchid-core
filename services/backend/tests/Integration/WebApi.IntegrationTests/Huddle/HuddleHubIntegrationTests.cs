// using System.Collections.Concurrent;
// using Microsoft.AspNetCore.Mvc.Testing;
// using Microsoft.AspNetCore.SignalR.Client;
// using Microsoft.AspNetCore.Http.Connections;
// using Microsoft.Extensions.DependencyInjection;
// using Application.Interfaces;
// using Moq;
// using WebApi.Hubs;
//
// namespace WebApi.IntegrationTests.Huddle;
//
// public class HuddleHubIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
// {
//     private readonly WebApplicationFactory<Program> _factory;
//     private readonly Mock<IHuddleRecordingService> _recordingMock = new();
//
//     public HuddleHubIntegrationTests(WebApplicationFactory<Program> factory)
//     {
//         _factory = factory.WithWebHostBuilder(builder =>
//         {
//             builder.UseSetting("Environment", "Testing");
//             builder.ConfigureServices(services =>
//             {
//                 services.AddSingleton<IHuddleRecordingService>(_recordingMock.Object);
//             });
//         });
//     }
//
//     [Fact]
//     public async Task Offer_IsForwarded_BetweenClients_AndSegmentStored()
//     {
//         var client = _factory.CreateClient();
//         var url = new Uri(client.BaseAddress!, "/huddleHub");
//
//         var conn1 = new HubConnectionBuilder()
//             .WithUrl(url, opts =>
//             {
//                 opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
//                 opts.Transports = HttpTransportType.LongPolling;
//             })
//             .Build();
//         var conn2 = new HubConnectionBuilder()
//             .WithUrl(url, opts =>
//             {
//                 opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
//                 opts.Transports = HttpTransportType.LongPolling;
//             })
//             .Build();
//
//         var offers = new ConcurrentQueue<string>();
//         conn2.On<string>("ReceiveOffer", o => offers.Enqueue(o));
//
//         await conn1.StartAsync();
//         await conn2.StartAsync();
//         await conn1.InvokeAsync("JoinRoom", "room");
//         await conn2.InvokeAsync("JoinRoom", "room");
//
//         await conn1.InvokeAsync("SendOffer", "room", "my-offer");
//         var start = DateTime.UtcNow;
//         while (offers.Count == 0 && DateTime.UtcNow - start < TimeSpan.FromSeconds(5))
//             await Task.Delay(100);
//
//         Assert.True(offers.TryDequeue(out var received));
//         Assert.Equal("my-offer", received);
//
//         await conn1.InvokeAsync("SendVideoSegment", "room", new byte[] {1,2,3});
//         _recordingMock.Verify(s => s.StoreSegmentAsync("room", It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
//
//         await conn1.DisposeAsync();
//         await conn2.DisposeAsync();
//     }
// }

