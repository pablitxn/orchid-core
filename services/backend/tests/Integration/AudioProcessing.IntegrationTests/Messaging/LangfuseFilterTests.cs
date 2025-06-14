// using System.Collections.Generic;
// using System.Net;
// using System.Net.Http;
// using System.Net.Http.Json;
// using System.Threading;
// using System.Threading.Tasks;
// using Infrastructure;
// using Infrastructure.Telemetry;
// using MassTransit;
// using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.DependencyInjection;
// using Xunit;
//
// namespace Workers.IntegrationTests.Messaging;
//
// public class LangfuseFilterTests
// {
//     [Fact]
//     public async Task ConsumeFilter_InvokesLangfuseClient()
//     {
//         var recordingHandler = new RecordingHandler();
//         var config = new ConfigurationBuilder()
//             .AddInMemoryCollection(new Dictionary<string, string?>
//             {
//                 ["Langfuse:ApiKey"] = "test",
//                 ["Langfuse:BaseUrl"] = "https://fake"
//             })
//             .Build();
//
//         var services = new ServiceCollection();
//         services.AddLangfuseTelemetry(config);
//         services.AddHttpClient<ILangfuseClient, LangfuseClient>()
//             .ConfigurePrimaryHttpMessageHandler(() => recordingHandler);
//
//         services.AddMassTransit(x =>
//         {
//             x.AddConsumer<DummyConsumer>();
//             x.UsingInMemory((context, cfg) =>
//             {
//                 cfg.UseConsumeFilter(typeof(LangfuseConsumeFilter<>), context);
//             });
//         });
//
//         await using var provider = services.BuildServiceProvider(true);
//         var bus = provider.GetRequiredService<IBusControl>();
//         await bus.StartAsync();
//         try
//         {
//             await bus.Publish(new DummyMessage());
//             await Task.Delay(200);
//         }
//         finally
//         {
//             await bus.StopAsync();
//         }
//
//         Assert.Contains(recordingHandler.Requests, r => r.RequestUri?.AbsolutePath == "/v1/traces");
//         Assert.Contains(recordingHandler.Requests, r => r.RequestUri?.AbsolutePath?.Contains("/end") == true);
//     }
//
//     record DummyMessage;
//
//     class DummyConsumer : IConsumer<DummyMessage>
//     {
//         public Task Consume(ConsumeContext<DummyMessage> context) => Task.CompletedTask;
//     }
//
//     class RecordingHandler : HttpMessageHandler
//     {
//         public List<HttpRequestMessage> Requests { get; } = new();
//
//         protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
//         {
//             Requests.Add(request);
//             var resp = new HttpResponseMessage(HttpStatusCode.OK)
//             {
//                 Content = request.RequestUri?.AbsolutePath switch
//                 {
//                     "/v1/traces" => JsonContent.Create(new { id = "trace" }),
//                     "/v1/spans" => JsonContent.Create(new { id = "span" }),
//                     _ => null
//                 }
//             };
//             return Task.FromResult(resp);
//         }
//     }
// }
//
