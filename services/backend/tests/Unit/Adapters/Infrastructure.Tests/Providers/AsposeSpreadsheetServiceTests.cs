using Aspose.Cells;
using Application.Interfaces;
using Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using Moq;

namespace Infrastructure.Tests.Providers;

public class AsposeSpreadsheetServiceTests : IDisposable
{
    private readonly LocalFileStorageService _fileStorageService;
    private readonly AsposeSpreadsheetService _spreadsheetService;
    private readonly string _tempFolder;

    public AsposeSpreadsheetServiceTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempFolder);
        _fileStorageService = new LocalFileStorageService(_tempFolder);
        var loggerMock = new Mock<ILogger<AsposeSpreadsheetService>>();
        _spreadsheetService = new AsposeSpreadsheetService(_fileStorageService, loggerMock.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempFolder))
            Directory.Delete(_tempFolder, true);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsExpectedSummary()
    {
        // Arrange
        var fileName = "test.xlsx";
        // Create a simple workbook with 3 data rows
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.Cells[0, 0].PutValue("A1");
        sheet.Cells[1, 0].PutValue("A2");
        sheet.Cells[2, 0].PutValue("A3");
        using var ms = new MemoryStream();
        workbook.Save(ms, SaveFormat.Xlsx);
        ms.Position = 0;
        // Store file in local storage
        await _fileStorageService.StoreFileAsync(ms, fileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        // Act
        var summary = await _spreadsheetService.ProcessAsync(string.Empty, fileName, CancellationToken.None);

        // Assert
        Assert.Contains($"Processed spreadsheet: {fileName}", summary);
        Assert.Contains("Sheet 'Sheet1' has 3 rows.", summary);
    }

    [Fact]
    public async Task ProcessAsync_MultipleSheets_ReturnsSummaryForAllSheets()
    {
        var fileName = "multi.xlsx";
        var workbook = new Workbook();
        var sheet1 = workbook.Worksheets[0];
        sheet1.Cells[0, 0].PutValue("row");
        sheet1.Cells[1, 0].PutValue("row");

        var sheet2Index = workbook.Worksheets.Add();
        var sheet2 = workbook.Worksheets[sheet2Index];
        sheet2.Name = "Second";
        sheet2.Cells[0, 0].PutValue("x");

        using var ms = new MemoryStream();
        workbook.Save(ms, SaveFormat.Xlsx);
        ms.Position = 0;
        await _fileStorageService.StoreFileAsync(ms, fileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var summary = await _spreadsheetService.ProcessAsync(string.Empty, fileName, CancellationToken.None);

        Assert.Contains("Sheet 'Sheet1' has 2 rows.", summary);
        Assert.Contains("Sheet 'Second' has 1 rows.", summary);
    }

    [Fact]
    public async Task ProcessAsync_WhenFileNotFound_LogsErrorAndThrows()
    {
        var storage = new Mock<IFileStorageService>();
        var logger = new Mock<ILogger<AsposeSpreadsheetService>>();
        var service = new AsposeSpreadsheetService(storage.Object, logger.Object);

        storage.Setup(s => s.GetFileAsync("missing.xlsx", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            service.ProcessAsync(string.Empty, "missing.xlsx", CancellationToken.None));

        logger.Verify(l => l.Log(
                Microsoft.Extensions.Logging.LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((_, _) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((_, _) => true)),
            Times.Once);
    }
}
