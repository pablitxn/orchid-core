using System.Security.Cryptography;
using System.Text;
using Application.Interfaces;
using Application.UseCases.Billing.GetBillingInvoices;
using Infrastructure.Stripe;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Infrastructure.Tests.Stripe;

public class StripeServiceTests
{
    private const string Payload = """
        {"id":"evt_synthetic","object":"event","request":null,"type":"customer.created","data":{"object":{"id":"cus_synthetic","object":"customer"}}}
        """;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void ValidateAndParseWebhook_UnconfiguredSecret_RejectsPredictableSignature(string? secret)
    {
        var service = CreateService(secret);
        var signature = Sign(Payload, secret ?? "");

        var result = service.ValidateAndParseWebhook(Payload, signature);

        Assert.Null(result);
    }

    [Fact]
    public void ValidateAndParseWebhook_ConfiguredSecret_AcceptsMatchingSignature()
    {
        const string secret = "synthetic-webhook-test-secret";
        var service = CreateService(secret);

        var result = service.ValidateAndParseWebhook(Payload, Sign(Payload, secret));

        Assert.NotNull(result);
        Assert.Equal("evt_synthetic", result.EventId);
    }

    [Fact]
    public void ValidateAndParseWebhook_ConfiguredSecret_RejectsWrongSignature()
    {
        var service = CreateService("synthetic-webhook-test-secret");

        var result = service.ValidateAndParseWebhook(Payload, Sign(Payload, "different-synthetic-secret"));

        Assert.Null(result);
    }

    [Fact]
    public void ParseWebhookPayload_NoCredentials_ParsesStoredEvent()
    {
        var service = CreateService("");

        var result = service.ParseWebhookPayload(Payload);

        Assert.NotNull(result);
        Assert.Equal("evt_synthetic", result.EventId);
    }

    [Fact]
    public void ValidateAndParseWebhook_ModifiedPayload_RejectsOriginalSignature()
    {
        const string secret = "synthetic-webhook-test-secret";
        var service = CreateService(secret);

        var result = service.ValidateAndParseWebhook(
            Payload.Replace("cus_synthetic", "cus_modified"), Sign(Payload, secret));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBillingInvoices_NoCustomerOrCredentials_ReturnsEmptyList()
    {
        var handler = new GetBillingInvoicesHandler(
            Mock.Of<ISubscriptionRepository>(),
            CreateService(""),
            NullLogger<GetBillingInvoicesHandler>.Instance);

        var result = await handler.Handle(new GetBillingInvoicesQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(result);
    }

    private static StripeService CreateService(string? secret) => new(
        Options.Create(new StripeSettings
        {
            SecretKey = "",
            PublishableKey = "",
            WebhookSecret = secret!
        }),
        NullLogger<StripeService>.Instance);

    private static string Sign(string payload, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var digest = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexString(digest).ToLowerInvariant()}";
    }
}
