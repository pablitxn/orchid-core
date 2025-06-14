using System.Diagnostics;
using Xunit;

namespace SpreadsheetCLI.IntegrationTests;

public class TestDataFixture : IAsyncLifetime
{
    public string TestDataPath { get; private set; } = string.Empty;
    public string CLIPath { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Setup test data directory
        var projectDir = GetProjectDirectory();
        TestDataPath = Path.Combine(projectDir, "TestData");
        Directory.CreateDirectory(TestDataPath);

        // Create test Excel files
        await CreateTestExcelFiles();

        // Build the CLI once for all tests
        await BuildCLI();
    }

    public Task DisposeAsync()
    {
        // Cleanup is handled by test runner
        return Task.CompletedTask;
    }

    public string GetTestFile(string fileName)
    {
        return Path.Combine(TestDataPath, fileName);
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
        CLIPath = Path.Combine(outputDir, "bin", "Debug", "net8.0", "ssllm");
        
        if (!File.Exists(CLIPath))
        {
            // Try with .exe extension on Windows
            CLIPath += ".exe";
        }

        if (!File.Exists(CLIPath))
        {
            throw new FileNotFoundException($"CLI executable not found at {CLIPath}");
        }
    }

    private async Task CreateTestExcelFiles()
    {
        // Create financial data CSV
        var financialData = @"Department,Q1,Q2,Q3,Q4,Total
Sales,100000,120000,110000,150000,480000
Marketing,50000,55000,60000,65000,230000
Engineering,200000,210000,220000,230000,860000
Operations,80000,85000,90000,95000,350000
HR,40000,42000,44000,46000,172000";

        await File.WriteAllTextAsync(GetTestFile("financial_data.csv"), financialData);

        // Create multi-table data CSV
        var multiTableData = @"Region Sales Q4 2023
Region,October,November,December,Total
North,45000,50000,55000,150000
South,38000,42000,45000,125000
East,41000,43000,48000,132000
West,52000,58000,65000,175000

Product Performance Q4 2023
Product,Units Sold,Revenue,Profit Margin
Product A,1250,125000,22%
Product B,980,98000,18%
Product C,1500,225000,25%
Product D,750,56250,15%";

        await File.WriteAllTextAsync(GetTestFile("multi_table_data.csv"), multiTableData);

        // Create mock Excel files (minimal valid XLSX structure)
        // In production tests, use a proper Excel library like ClosedXML
        CreateMockExcelFile(GetTestFile("financial_data.xlsx"));
        CreateMockExcelFile(GetTestFile("multi_table_data.xlsx"));
        CreateMockExcelFile(GetTestFile("test_data.xlsx"));
    }

    private void CreateMockExcelFile(string path)
    {
        // Create a minimal valid XLSX file
        // This is a ZIP file with Excel structure
        // In real tests, use a proper Excel library
        
        // For now, create an empty file that will be recognized as XLSX
        var zipHeader = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        File.WriteAllBytes(path, zipHeader);
    }

    private string GetProjectDirectory()
    {
        var assemblyLocation = typeof(TestDataFixture).Assembly.Location;
        var directory = new DirectoryInfo(assemblyLocation);
        
        while (directory != null && !directory.GetFiles("*.csproj").Any())
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find project directory");
    }
}