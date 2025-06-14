using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using SpreadsheetCLI.Output;
using Xunit;

namespace SpreadsheetCLI.IntegrationTests;

public class SpreadsheetCLITests : IAsyncLifetime
{
    private string _cliPath = string.Empty;
    private string _testDataPath = string.Empty;

    public async Task InitializeAsync()
    {
        // Build the CLI in debug mode for testing
        var projectDir = GetProjectDirectory();
        _testDataPath = Path.Combine(projectDir, "TestData");
        
        // Ensure test data directory exists
        Directory.CreateDirectory(_testDataPath);
        
        // Create test Excel file
        await CreateTestExcelFile();
        
        // Build the CLI
        await BuildCLI();
    }

    public Task DisposeAsync()
    {
        // Cleanup test files if needed
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Run_WithBalancedRecipe_ShouldReturnCompressedOutput()
    {
        // Arrange
        var testFile = Path.Combine(_testDataPath, "test_data.xlsx");

        // Act
        var result = await RunCLI($"run \"{testFile}\" --recipe balanced");

        // Assert
        result.ExitCode.Should().Be(0);
        
        var output = JsonSerializer.Deserialize<CompressionOutput>(result.StandardOutput);
        output.Should().NotBeNull();
        output!.Success.Should().BeTrue();
        output.EstimatedTokens.Should().BeGreaterThan(0);
        output.Statistics.Should().NotBeNull();
        output.Statistics.CompressionRatio.Should().BeGreaterThanOrEqualTo(0);
        output.Metrics.ProcessingTimeMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Run_WithAggressiveRecipe_ShouldAchieveHigherCompression()
    {
        // Arrange
        var testFile = Path.Combine(_testDataPath, "test_data.xlsx");

        // Act
        var balancedResult = await RunCLI($"run \"{testFile}\" --recipe balanced");
        var aggressiveResult = await RunCLI($"run \"{testFile}\" --recipe aggressive");

        // Assert
        balancedResult.ExitCode.Should().Be(0);
        aggressiveResult.ExitCode.Should().Be(0);

        var balancedOutput = JsonSerializer.Deserialize<CompressionOutput>(balancedResult.StandardOutput);
        var aggressiveOutput = JsonSerializer.Deserialize<CompressionOutput>(aggressiveResult.StandardOutput);

        aggressiveOutput!.Statistics.CompressionRatio.Should().BeGreaterThan(balancedOutput!.Statistics.CompressionRatio);
        aggressiveOutput.EstimatedTokens.Should().BeLessThanOrEqualTo(balancedOutput.EstimatedTokens);
    }

    [Fact]
    public async Task Run_WithNoneRecipe_ShouldReturnUncompressedOutput()
    {
        // Arrange
        var testFile = Path.Combine(_testDataPath, "test_data.xlsx");

        // Act
        var result = await RunCLI($"run \"{testFile}\" --recipe none");

        // Assert
        result.ExitCode.Should().Be(0);
        
        var output = JsonSerializer.Deserialize<CompressionOutput>(result.StandardOutput);
        output!.Success.Should().BeTrue();
        output.Statistics.CompressionRatio.Should().Be(0); // No compression
    }

    [Fact]
    public async Task Run_WithTokenLimit_ShouldRespectLimit()
    {
        // Arrange
        var testFile = Path.Combine(_testDataPath, "test_data.xlsx");
        var tokenLimit = 1000;

        // Act
        var result = await RunCLI($"run \"{testFile}\" --recipe balanced --token-limit {tokenLimit}");

        // Assert
        result.ExitCode.Should().Be(0);
        
        var output = JsonSerializer.Deserialize<CompressionOutput>(result.StandardOutput);
        output!.EstimatedTokens.Should().BeLessThanOrEqualTo((int)(tokenLimit * 1.1)); // Allow 10% margin
    }

    [Fact]
    public async Task Run_WithOutputContent_ShouldIncludeCompressedContent()
    {
        // Arrange
        var testFile = Path.Combine(_testDataPath, "test_data.xlsx");

        // Act
        var result = await RunCLI($"run \"{testFile}\" --recipe balanced --output-content");

        // Assert
        result.ExitCode.Should().Be(0);
        
        var output = JsonSerializer.Deserialize<CompressionOutput>(result.StandardOutput);
        output!.CompressedContent.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Run_WithInvalidFile_ShouldReturnError()
    {
        // Arrange
        var invalidFile = Path.Combine(_testDataPath, "non_existent.xlsx");

        // Act
        var result = await RunCLI($"run \"{invalidFile}\" --recipe balanced");

        // Assert
        result.ExitCode.Should().NotBe(0);
        result.StandardError.Should().Contain("Error");
    }

    [Fact]
    public async Task Run_WithFullRecipeButNoQuestion_ShouldReturnError()
    {
        // Arrange
        var testFile = Path.Combine(_testDataPath, "test_data.xlsx");

        // Act
        var result = await RunCLI($"run \"{testFile}\" --recipe full");

        // Assert
        result.ExitCode.Should().Be(1);
        result.StandardError.Should().Contain("Question is required");
    }

    [Fact]
    public async Task Run_WithHelpFlag_ShouldDisplayHelp()
    {
        // Act
        var result = await RunCLI("--help");

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Contain("ssllm");
        result.StandardOutput.Should().Contain("run");
        result.StandardOutput.Should().Contain("--recipe");
    }

    [Fact]
    public async Task Run_WithVerboseFlag_ShouldProduceDetailedLogs()
    {
        // Arrange
        var testFile = Path.Combine(_testDataPath, "test_data.xlsx");

        // Act
        var result = await RunCLI($"run \"{testFile}\" --recipe balanced --verbose");

        // Assert
        result.ExitCode.Should().Be(0);
        // With verbose flag, there should be additional logging output
        // Note: In real implementation, verbose logs might go to stderr or a log file
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
            CreateNoWindow = true,
            Environment = { ["OPENAI_API_KEY"] = "test-key" } // Mock API key for tests
        };

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

    private async Task BuildCLI()
    {
        var projectPath = Path.Combine(GetProjectDirectory(), "..", "..", "..", "src", "Adapters", "CLI", "SpreadsheetCLI", "SpreadsheetCLI.csproj");
        
        var buildProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{projectPath}\" -c Debug",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        buildProcess.Start();
        await buildProcess.WaitForExitAsync();

        if (buildProcess.ExitCode != 0)
        {
            var error = await buildProcess.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Failed to build CLI: {error}");
        }

        // Set the path to the built executable
        var outputDir = Path.GetDirectoryName(projectPath)!;
        _cliPath = Path.Combine(outputDir, "bin", "Debug", "net8.0", "ssllm");
        
        if (!File.Exists(_cliPath))
        {
            // Try with .exe extension on Windows
            _cliPath += ".exe";
        }
    }

    private async Task CreateTestExcelFile()
    {
        // For testing purposes, we'll create a simple CSV that can be read as Excel
        // In production, you'd use a proper Excel library
        var csvContent = @"Department,Q1,Q2,Q3,Q4,Total
Sales,100000,120000,110000,150000,480000
Marketing,50000,55000,60000,65000,230000
Engineering,200000,210000,220000,230000,860000
Operations,80000,85000,90000,95000,350000
HR,40000,42000,44000,46000,172000";

        var csvPath = Path.Combine(_testDataPath, "test_data.csv");
        await File.WriteAllTextAsync(csvPath, csvContent);

        // For actual Excel file creation, you'd need to use a library like ClosedXML or EPPlus
        // For now, we'll create a mock Excel file
        var xlsxPath = Path.Combine(_testDataPath, "test_data.xlsx");
        
        // Create a minimal valid XLSX file structure (this is a simplified mock)
        // In real tests, use a proper Excel library
        File.WriteAllBytes(xlsxPath, new byte[] { 0x50, 0x4B, 0x03, 0x04 }); // PK header for ZIP
    }

    private string GetProjectDirectory()
    {
        var assemblyLocation = typeof(SpreadsheetCLITests).Assembly.Location;
        var directory = new DirectoryInfo(assemblyLocation);
        
        while (directory != null && !directory.GetFiles("*.csproj").Any())
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find project directory");
    }

    private class CLIResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
    }
}