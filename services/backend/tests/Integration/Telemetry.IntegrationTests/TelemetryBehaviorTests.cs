// using Infrastructure.Telemetry;
// using MediatR;
// using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.DependencyInjection;
// using System.Collections.Generic;
// using System.Linq;
// using System.Net;
// using System.Net.Http;
// using System.Net.Http.Json;
// using System.Threading;
// using System.Threading.Tasks;
// using Xunit;
//
// namespace Telemetry.IntegrationTests;
//
// public class TelemetryBehaviorTests
// {
//     [Fact]
//     public void AddLangfuseTelemetry_RegistersClientAndBehavior()
//     {
//         var services = new ServiceCollection();
//         var config = new ConfigurationBuilder()
//             .AddInMemoryCollection(new Dictionary<string, string?>
//             {
//                 ["Langfuse:ApiKey"] = "test",
//                 ["Langfuse:BaseUrl"] = "https://fake",
//             })
//             .Build();
//
//         services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(TelemetryBehaviorTests).Assembly));
//         services.AddLangfuseTelemetry(config);
//
//         using var provider = services.BuildServiceProvider();
//
//         var client = provider.GetService<ILangfuseClient>();
//         Assert.NotNull(client);
//
//         var behaviors = provider.GetServices<IPipelineBehavior<SampleRequest, string>>();
//         Assert.Contains(behaviors, b => b.GetType().Name.StartsWith(nameof(LangfuseTelemetryBehavior<int,int>).Split('`')[0]));
//     }
//
//     [Fact]
//     public async Task TelemetryBehavior_InvokesLangfuseClient()
//     {
//         var services = new ServiceCollection();
//         var config = new ConfigurationBuilder()
//             .AddInMemoryCollection(new Dictionary<string, string?>
//             {
//                 ["Langfuse:ApiKey"] = "test",
//                 ["Langfuse:BaseUrl"] = "https://fake",
//             })
//             .Build();
//
//         services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SampleHandler).Assembly));
//         services.AddLangfuseTelemetry(config);
//
//         var recordingHandler = new RecordingHandler();
//         services.AddHttpClient<ILangfuseClient, LangfuseClient>()
//             .ConfigurePrimaryHttpMessageHandler(() => recordingHandler);
//
//         using var provider = services.BuildServiceProvider();
//         var mediator = provider.GetRequiredService<IMediator>();
//
//         var response = await mediator.Send(new SampleRequest("ping"));
//         Assert.Equal("ping", response);
//
//         Assert.Contains(recordingHandler.Requests, r => r.RequestUri?.AbsolutePath == "/v1/traces");
//         Assert.Contains(recordingHandler.Requests, r => r.RequestUri?.AbsolutePath?.Contains("/end") == true);
//     }
//
//     public record SampleRequest(string Message) : IRequest<string>;
//
//     public class SampleHandler : IRequestHandler<SampleRequest, string>
//     {
//         public Task<string> Handle(SampleRequest request, CancellationToken cancellationToken)
//             => Task.FromResult(request.Message);
//     }
//
//     private class RecordingHandler : HttpMessageHandler
//     {
//         public List<HttpRequestMessage> Requests { get; } = new();
//
//         protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
//         {
//             Requests.Add(request);
//             var path = request.RequestUri?.AbsolutePath;
//             HttpResponseMessage response = new(HttpStatusCode.OK)
//             {
//                 Content = path switch
//                 {
//                     "/v1/traces" => JsonContent.Create(new { id = "trace" }),
//                     "/v1/spans" => JsonContent.Create(new { id = "span" }),
//                     _ => null,
//                 }
//             };
//             return Task.FromResult(response);
//         }
//     }
// }

