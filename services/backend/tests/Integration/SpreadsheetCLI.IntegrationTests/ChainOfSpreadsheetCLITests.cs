using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using SpreadsheetCLI.Output;
using Xunit;

namespace SpreadsheetCLI.IntegrationTests;

public class ChainOfSpreadsheetCLITests : IClassFixture<TestDataFixture>
{
    private readonly TestDataFixture _fixture;
    private readonly string _cliPath;

    public ChainOfSpreadsheetCLITests(TestDataFixture fixture)
    {
        _fixture = fixture;
        _cliPath = fixture.CLIPath;
    }

    [SkippableFact]
    public async Task Run_WithFullRecipeAndQuestion_ShouldReturnAnswer()
    {
        Skip.If(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")), 
            "OpenAI API key not configured");

        // Arrange
        var testFile = _fixture.GetTestFile("financial_data.xlsx");
        var question = "What is the total revenue for Q4?";

        // Act
        var result = await RunCLI($"run \"{testFile}\" --recipe full --question \"{question}\"");

        // Assert
        result.ExitCode.Should().Be(0);
        
        var output = JsonSerializer.Deserialize<ChainOfSpreadsheetOutput>(result.StandardOutput);
        output.Should().NotBeNull();
        output!.Success.Should().BeTrue();
        output.Answer.Should().NotBeNullOrEmpty();
        output.DetectedTable.Should().NotBeNullOrEmpty();
        output.Metrics.ProcessingTimeMs.Should().BeGreaterThan(0);
        output.Metrics.TotalCost.Should().BeGreaterThan(0);
    }

    [SkippableFact]
    public async Task Run_WithFullRecipeAndTrace_ShouldIncludeDetailedTrace()
    {
        Skip.If(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")), 
            "OpenAI API key not configured");

        // Arrange
        var testFile = _fixture.GetTestFile("financial_data.xlsx");
        var question = "Which department has the highest expenses?";

        // Act
        var result = await RunCLI($"run \"{testFile}\" --recipe full --question \"{question}\" --include-trace");

        // Assert
        result.ExitCode.Should().Be(0);
        
        var output = JsonSerializer.Deserialize<ChainOfSpreadsheetOutput>(result.StandardOutput);
        output!.Trace.Should().NotBeNull();
        output.Trace!.TableDetection.Should().NotBeNull();
        output.Trace.QuestionAnswering.Should().NotBeNull();
        output.Trace.TotalDuration.Should().NotBeNullOrEmpty();
        output.Trace.TotalCost.Should().BeGreaterThan(0);
    }

    [SkippableFact]
    public async Task Run_WithComplexQuestion_ShouldHandleMultiTableScenarios()
    {
        Skip.If(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")), 
            "OpenAI API key not configured");

        // Arrange
        var testFile = _fixture.GetTestFile("multi_table_data.xlsx");
        var question = "Compare sales performance between regions in the last quarter";

        // Act
        var result = await RunCLI($"run \"{testFile}\" --recipe full --question \"{question}\"");

        // Assert
        result.ExitCode.Should().Be(0);
        
        var output = JsonSerializer.Deserialize<ChainOfSpreadsheetOutput>(result.StandardOutput);
        output!.Success.Should().BeTrue();
        output.Answer.Should().NotBeNullOrEmpty();
        // The answer should contain comparative information
        output.Answer.Should().ContainAny("region", "sales", "quarter");
    }

    private async Task<CLIResult> RunCLI(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _cliPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Use real API key if available for integration tests
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
        {
            startInfo.Environment["OPENAI_API_KEY"] = apiKey;
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();

        return new CLIResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = output,
            StandardError = error
        };
    }

    private class CLIResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
    }
}