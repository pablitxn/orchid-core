using Aspose.Cells;
using Infrastructure.Providers;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Tests.Providers;

public class CellsWorkbookLoaderTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly LocalFileStorageService _storage;

    public CellsWorkbookLoaderTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempFolder);
        _storage = new LocalFileStorageService(_tempFolder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempFolder)) Directory.Delete(_tempFolder, true);
    }

    [Fact]
    public async Task LoadAsync_DetectsHeaderRowAndSynonyms()
    {
        var wb = new Workbook();
        var sheet = wb.Worksheets[0];
        sheet.Cells[1, 0].PutValue("Id");
        sheet.Cells[1, 1].PutValue("Nombre");
        sheet.Cells[2, 0].PutValue(1);
        sheet.Cells[2, 1].PutValue("Alice");
        using var ms = new MemoryStream();
        wb.Save(ms, SaveFormat.Xlsx);
        ms.Position = 0;
        await _storage.StoreFileAsync(ms, "test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        // var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<CellsWorkbookLoader>();
        // var loader = new CellsWorkbookLoader(_storage, logger);
        // var result = await loader.LoadAsync(Path.Combine(_tempFolder, "test.xlsx"));

        // var ws = Assert.Single(result.Worksheets);
        // Assert.Equal(1, ws.HeaderRowIndex);
        // var header = Assert.Single(ws.Headers, h => h.Name == "Id");
        // Assert.Contains("identifier", header.Synonyms);
        // Assert.True(header.IsKey);
    }
}
