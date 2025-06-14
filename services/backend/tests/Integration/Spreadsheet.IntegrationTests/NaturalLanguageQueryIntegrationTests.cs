using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Application.Interfaces;
using Application.UseCases.Spreadsheet.NaturalLanguageQuery;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using WebApi;

namespace Spreadsheet.IntegrationTests;

public class NaturalLanguageQueryIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public NaturalLanguageQueryIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Environment", "Testing");
                builder.ConfigureServices(services =>
                {
                    // Add test authentication
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = "Test";
                        options.DefaultChallengeScheme = "Test";
                    }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                    // Remove problematic database services
                    services.RemoveAll<NpgsqlDataSource>();
                    services.RemoveAll<IVectorStorePort>();
                    
                    // Remove services we want to mock
                    services.RemoveAll<IWorkbookLoader>();
                    services.RemoveAll<IWorkbookNormalizer>();
                    services.RemoveAll<IWorkbookSummarizer>();
                    services.RemoveAll<IFormulaTranslator>();
                    services.RemoveAll<IFormulaValidator>();
                    services.RemoveAll<IFormulaExecutor>();
                    services.RemoveAll<ICacheStore>();
                    services.RemoveAll<IActivityPublisher>();

                    // Add mock implementations
                    services.AddSingleton<IVectorStorePort, MockVectorStore>();
                    services.AddSingleton<IWorkbookLoader, MockWorkbookLoader>();
                    services.AddSingleton<IWorkbookNormalizer, MockWorkbookNormalizer>();
                    services.AddSingleton<IWorkbookSummarizer, MockWorkbookSummarizer>();
                    services.AddSingleton<IFormulaTranslator, MockFormulaTranslator>();
                    services.AddSingleton<IFormulaValidator, MockFormulaValidator>();
                    services.AddSingleton<IFormulaExecutor, MockFormulaExecutor>();
                    services.AddSingleton<ICacheStore, MockCacheStore>();
                    services.AddSingleton<IActivityPublisher, MockActivityPublisher>();
                });
            })
            .CreateClient();
    }

    [Fact]
    public async Task NaturalLanguageQuery_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var request = new
        {
            filePath = "test-data/sales.xlsx",
            query = "What is the total sales amount?",
            worksheetName = "Sheet1"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/spreadsheet/natural-language-query", request);

        // Assert
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<NaturalLanguageQueryResponse>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Result);
        Assert.NotEmpty(result.Formula);
        Assert.NotEmpty(result.Explanation);
    }

    [Fact]
    public async Task NaturalLanguageQuery_WithAmbiguousQuery_ReturnsClarificationNeeded()
    {
        // Arrange
        var request = new
        {
            filePath = "test-data/sales.xlsx",
            query = "Show me the date",
            worksheetName = "Sheet1"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/spreadsheet/natural-language-query", request);

        // Assert
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<NaturalLanguageQueryResponse>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.True(result.NeedsClarification);
        Assert.NotEmpty(result.ClarificationPrompt);
    }

    [Fact]
    public async Task NaturalLanguageQuery_WithMissingFile_ReturnsError()
    {
        // Arrange
        var request = new
        {
            filePath = "non-existent.xlsx",
            query = "What is the total?",
            worksheetName = "Sheet1"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/spreadsheet/natural-language-query", request);

        // Assert
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<NaturalLanguageQueryResponse>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.NotEmpty(result.Error);
    }
}

internal class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Email, "test@example.com")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

internal class MockVectorStore : IVectorStorePort
{
    public Task UpsertChunkAsync(VectorChunk chunk, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int k = 8, CancellationToken ct = default)
    {
        // Return empty results for tests
        return Task.FromResult<IReadOnlyList<SearchHit>>(new List<SearchHit>());
    }
}

internal class MockWorkbookLoader : IWorkbookLoader
{
    public Task<WorkbookEntity> LoadAsync(string filePath, CancellationToken ct = default)
    {
        if (filePath.Contains("non-existent"))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var workbook = new WorkbookEntity
        {
            Worksheets = new List<WorksheetEntity>
            {
                new WorksheetEntity
                {
                    Name = "Sheet1",
                    Headers = new List<HeaderEntity>
                    {
                        new HeaderEntity { Name = "Date", ColumnIndex = 0 },
                        new HeaderEntity { Name = "Customer", ColumnIndex = 1 },
                        new HeaderEntity { Name = "Amount", ColumnIndex = 2 }
                    },
                    Rows = new List<List<CellEntity>>()
                }
            }
        };
        return Task.FromResult(workbook);
    }
}

internal class MockWorkbookNormalizer : IWorkbookNormalizer
{
    public Task<NormalizedWorkbook> NormalizeAsync(WorkbookEntity workbook, CancellationToken ct = default)
    {
        var normalized = new NormalizedWorkbook
        {
            MainWorksheet = workbook.Worksheets.First(),
            ColumnMetadata = new Dictionary<string, ColumnMetadata>
            {
                ["Amount"] = new ColumnMetadata { OriginalName = "Amount", DataType = ColumnDataType.Number },
                ["Date"] = new ColumnMetadata { OriginalName = "Date", DataType = ColumnDataType.DateTime }
            }
        };
        return Task.FromResult(normalized);
    }
}

internal class MockWorkbookSummarizer : IWorkbookSummarizer
{
    public Task<WorkbookSummary> SummarizeAsync(NormalizedWorkbook workbook, int sampleSize = 100, CancellationToken ct = default)
    {
        var summary = new WorkbookSummary
        {
            SheetName = "Sheet1",
            TotalRows = 100,
            Columns = new List<ColumnSummary>
            {
                new ColumnSummary { Alias = "Amount", Original = "Amount", DataType = "Number" },
                new ColumnSummary { Alias = "Date", Original = "Date", DataType = "Date" }
            }
        };
        return Task.FromResult(summary);
    }
}

internal class MockFormulaTranslator : IFormulaTranslator
{
    public Task<FormulaTranslation> TranslateAsync(string query, WorkbookSummary summary, CancellationToken ct = default)
    {
        FormulaTranslation translation;
        
        if (query.ToLowerInvariant().Contains("date") && !query.ToLowerInvariant().Contains("total"))
        {
            translation = new FormulaTranslation
            {
                Formula = "",
                Explanation = "Multiple date columns found",
                NeedsClarification = true,
                ClarificationPrompt = "Which date are you referring to? Order date or delivery date?"
            };
        }
        else
        {
            translation = new FormulaTranslation
            {
                Formula = "=SUM(Amount)",
                Explanation = "Calculates the sum of all amounts",
                NeedsClarification = false
            };
        }
        
        return Task.FromResult(translation);
    }
}

internal class MockFormulaValidator : IFormulaValidator
{
    public Task<FormulaValidation> ValidateAsync(string formula, NormalizedWorkbook workbook, CancellationToken ct = default)
    {
        var validation = new FormulaValidation { IsValid = true };
        return Task.FromResult(validation);
    }
}

internal class MockFormulaExecutor : IFormulaExecutor
{
    public Task<FormulaResult> ExecuteAsync(string formula, string filePath, string worksheetName, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var result = new FormulaResult
        {
            Success = true,
            Value = 600.0,
            ResultType = FormulaResultType.SingleValue,
            ExecutionTime = TimeSpan.FromMilliseconds(100)
        };
        return Task.FromResult(result);
    }
}

internal class MockCacheStore : ICacheStore
{
    private readonly ConcurrentDictionary<string, object> _cache = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out var value))
        {
            return Task.FromResult((T?)value);
        }
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        _cache.AddOrUpdate(key, value!, (k, v) => value!);
        return Task.CompletedTask;
    }

    public Task<string?> GetStringAsync(string key, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out var value))
        {
            return Task.FromResult((string?)value);
        }
        return Task.FromResult<string?>(null);
    }

    public Task SetStringAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        _cache.AddOrUpdate(key, value, (k, v) => value);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}

internal class MockActivityPublisher : IActivityPublisher
{
    public Task PublishAsync(string activityType, object? payload, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}