using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using Moq;

namespace Infrastructure.Tests.Providers;

public class WorkbookNormalizerTests
{
    private readonly WorkbookNormalizer _normalizer;
    private readonly Mock<ILogger<WorkbookNormalizer>> _loggerMock;

    public WorkbookNormalizerTests()
    {
        _loggerMock = new Mock<ILogger<WorkbookNormalizer>>();
        _normalizer = new WorkbookNormalizer(_loggerMock.Object);
    }

    [Fact]
    public async Task NormalizeAsync_DetectsMainTable_ReturnsNormalizedWorkbook()
    {
        // Arrange
        var workbook = CreateTestWorkbook();
        
        // Act
        var result = await _normalizer.NormalizeAsync(workbook);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("Sheet1", result.MainWorksheet.Name);
        Assert.Equal(3, result.ColumnMetadata.Count);
        Assert.True(result.AliasToOriginal.ContainsKey("Date"));
        Assert.True(result.AliasToOriginal.ContainsKey("Customer"));
        Assert.True(result.AliasToOriginal.ContainsKey("Amount"));
    }

    [Fact]
    public async Task NormalizeAsync_InfersColumnTypes_Correctly()
    {
        // Arrange
        var workbook = CreateTestWorkbook();
        
        // Act
        var result = await _normalizer.NormalizeAsync(workbook);
        
        // Assert
        var dateColumn = result.ColumnMetadata["Date"];
        Assert.Equal(ColumnDataType.DateTime, dateColumn.DataType);
        
        var customerColumn = result.ColumnMetadata["Customer"];
        Assert.Equal(ColumnDataType.String, customerColumn.DataType);
        
        var amountColumn = result.ColumnMetadata["Amount"];
        Assert.Equal(ColumnDataType.Number, amountColumn.DataType);
        Assert.Equal(100.0, amountColumn.MinValue);
        Assert.Equal(300.0, amountColumn.MaxValue);
        Assert.Equal(200.0, amountColumn.Mean);
    }

    [Fact]
    public async Task NormalizeAsync_HandlesSpanishHeaders_MapsToEnglishAliases()
    {
        // Arrange
        var workbook = new WorkbookEntity
        {
            Worksheets = new List<WorksheetEntity>
            {
                new WorksheetEntity
                {
                    Name = "Datos",
                    Headers = new List<HeaderEntity>
                    {
                        new HeaderEntity { Name = "Fecha", ColumnIndex = 0 },
                        new HeaderEntity { Name = "Cliente", ColumnIndex = 1 },
                        new HeaderEntity { Name = "Cantidad", ColumnIndex = 2 }
                    },
                    Rows = new List<List<CellEntity>>
                    {
                        new List<CellEntity>
                        {
                            new CellEntity { ColumnIndex = 0, Value = "2024-01-01" },
                            new CellEntity { ColumnIndex = 1, Value = "Juan" },
                            new CellEntity { ColumnIndex = 2, Value = "150" }
                        }
                    }
                }
            }
        };
        
        // Act
        var result = await _normalizer.NormalizeAsync(workbook);
        
        // Assert
        Assert.True(result.ColumnMetadata.ContainsKey("Date"));
        Assert.True(result.ColumnMetadata.ContainsKey("Customer"));
        Assert.True(result.ColumnMetadata.ContainsKey("Quantity"));
    }

    private static WorkbookEntity CreateTestWorkbook()
    {
        return new WorkbookEntity
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
                    Rows = new List<List<CellEntity>>
                    {
                        new List<CellEntity>
                        {
                            new CellEntity { ColumnIndex = 0, Value = "2024-01-01" },
                            new CellEntity { ColumnIndex = 1, Value = "John Doe" },
                            new CellEntity { ColumnIndex = 2, Value = "100" }
                        },
                        new List<CellEntity>
                        {
                            new CellEntity { ColumnIndex = 0, Value = "2024-01-02" },
                            new CellEntity { ColumnIndex = 1, Value = "Jane Smith" },
                            new CellEntity { ColumnIndex = 2, Value = "200" }
                        },
                        new List<CellEntity>
                        {
                            new CellEntity { ColumnIndex = 0, Value = "2024-01-03" },
                            new CellEntity { ColumnIndex = 1, Value = "Bob Johnson" },
                            new CellEntity { ColumnIndex = 2, Value = "300" }
                        }
                    }
                }
            }
        };
    }
}