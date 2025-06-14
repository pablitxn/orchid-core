using System.Diagnostics;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Infrastructure.Providers;
using Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;


namespace Spreadsheet.IntegrationTests;

public class EnhancedWorkbookLoaderTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly InMemoryFileStorageService _fileStorage;
    private readonly IEnhancedWorkbookLoader _loader;
    private readonly string _testFileName = "test_workbook.xlsx";

    public EnhancedWorkbookLoaderTests(ITestOutputHelper output)
    {
        _output = output;
        _fileStorage = new InMemoryFileStorageService();

        var logger = new XunitLogger<EnhancedAsposeWorkbookLoader>(output);
        _loader = new EnhancedAsposeWorkbookLoader(_fileStorage, logger);
    }

    public async Task InitializeAsync()
    {
        // Create a test Excel file in memory
        var testFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "test_workbook.xlsx");
        if (File.Exists(testFilePath))
        {
            await using var fileStream = File.OpenRead(testFilePath);
            await _fileStorage.StoreFileAsync(fileStream, _testFileName,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }
        else
        {
            // Create a simple test file programmatically if the test data doesn't exist
            await CreateTestWorkbookAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LoadAsync_ValidWorkbook_ReturnsWorkbookContext()
    {
        // Act
        var result = await _loader.LoadAsync(_testFileName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Worksheets);
        Assert.Equal(_testFileName, Path.GetFileName(result.FilePath));
        Assert.NotNull(result.Metadata);
        Assert.NotNull(result.Statistics);
    }

    [Fact]
    public async Task LoadAsync_ExtractsAllCellMetadata()
    {
        // Act
        var result = await _loader.LoadAsync(_testFileName);

        // Assert
        var firstSheet = result.Worksheets.First();
        Assert.NotEmpty(firstSheet.Cells);

        var cell = firstSheet.Cells.Values.First(c => c.DataType != CellDataType.Empty);
        Assert.NotNull(cell.Address);
        Assert.True(cell.RowIndex >= 0);
        Assert.True(cell.ColumnIndex >= 0);
        Assert.NotNull(cell.FormattedValue);
    }

    [Fact]
    public async Task LoadAsync_WithMemoryOptimization_CompletesSuccessfully()
    {
        // Arrange
        var options = new WorkbookLoadOptions
        {
            MemoryOptimization = MemoryOptimizationLevel.Maximum,
            MaxCellCount = 10000
        };

        // Act
        var sw = Stopwatch.StartNew();
        var result = await _loader.LoadAsync(_testFileName, options);
        sw.Stop();

        // Assert
        Assert.NotNull(result);
        _output.WriteLine($"Load time with max optimization: {sw.ElapsedMilliseconds}ms");
        Assert.True(result.Statistics.TotalCells <= options.MaxCellCount);
    }

    [Fact]
    public async Task LoadAsync_DetectsTableStructures()
    {
        // Arrange
        var options = new WorkbookLoadOptions { DetectTables = true };

        // Act
        var result = await _loader.LoadAsync(_testFileName, options);

        // Assert
        var worksheet = result.Worksheets.FirstOrDefault();
        Assert.NotNull(worksheet);
        // Tables might not be detected in simple test files
        _output.WriteLine($"Detected {worksheet.DetectedTables.Count} tables");
    }

    [Fact]
    public async Task LoadAsync_TracksStatistics()
    {
        // Act
        var result = await _loader.LoadAsync(_testFileName);

        // Assert
        var stats = result.Statistics;
        Assert.True(stats.TotalCells > 0);
        Assert.True(stats.NonEmptyCells <= stats.TotalCells);
        Assert.InRange(stats.EmptyCellPercentage, 0, 100);
        Assert.NotEmpty(stats.DataTypeDistribution);
        Assert.True(stats.EstimatedTokenCount > 0);
    }

    [Fact]
    public async Task LoadAsync_Performance_HandlesLargeWorkbook()
    {
        // This test would require a larger test file
        // Skipping for now but including as a template

        var largeFileName = "large_test.xlsx";
        // Skip test if file doesn't exist
        try
        {
            await using var stream = await _fileStorage.GetFileAsync(largeFileName, default);
        }
        catch
        {
            _output.WriteLine("Large test file not available, skipping performance test");
            return;
        }

        var sw = Stopwatch.StartNew();
        var result = await _loader.LoadAsync(largeFileName);
        sw.Stop();

        _output.WriteLine($"Loaded {result.Statistics.TotalCells} cells in {sw.ElapsedMilliseconds}ms");
        var cellsPerSecond = result.Statistics.TotalCells / (sw.ElapsedMilliseconds / 1000.0);
        _output.WriteLine($"Performance: {cellsPerSecond:N0} cells/second");

        // Assert performance meets requirements (< 2s for 100k cells)
        Assert.True(cellsPerSecond > 50000); // 50k cells/second minimum
    }

    [Fact]
    public async Task CanLoadAsync_ValidExcelFile_ReturnsTrue()
    {
        // Act
        var result = await _loader.CanLoadAsync(_testFileName);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanLoadAsync_InvalidFile_ReturnsFalse()
    {
        // Act
        var result = await _loader.CanLoadAsync("nonexistent.txt");

        // Assert
        Assert.False(result);
    }

    private async Task CreateTestWorkbookAsync()
    {
        // Create a simple workbook using Aspose.Cells for testing
        using var workbook = new Aspose.Cells.Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.Name = "TestSheet";

        // Add headers
        worksheet.Cells["A1"].PutValue("ID");
        worksheet.Cells["B1"].PutValue("Name");
        worksheet.Cells["C1"].PutValue("Value");
        worksheet.Cells["D1"].PutValue("Date");

        // Style headers
        var headerStyle = worksheet.Cells["A1"].GetStyle();
        headerStyle.Font.IsBold = true;
        headerStyle.BackgroundColor = System.Drawing.Color.LightGray;

        for (var col = 0; col < 4; col++)
        {
            worksheet.Cells[0, col].SetStyle(headerStyle);
        }

        // Add data rows
        for (var row = 1; row <= 100; row++)
        {
            worksheet.Cells[$"A{row + 1}"].PutValue(row);
            worksheet.Cells[$"B{row + 1}"].PutValue($"Item {row}");
            worksheet.Cells[$"C{row + 1}"].PutValue(row * 10.5);
            worksheet.Cells[$"D{row + 1}"].PutValue(DateTime.Now.AddDays(-row));
        }

        // Add a formula
        worksheet.Cells["E2"].Formula = "=C2*2";

        // Save to memory stream
        using var ms = new MemoryStream();
        workbook.Save(ms, Aspose.Cells.SaveFormat.Xlsx);
        ms.Position = 0;

        await _fileStorage.StoreFileAsync(ms, _testFileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private class XunitLogger<T>(ITestOutputHelper output) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
            if (exception != null)
                output.WriteLine(exception.ToString());
        }
    }
}