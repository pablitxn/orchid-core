using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Spreadsheet.IntegrationTests;

public class VanillaSerializerTests(ITestOutputHelper output)
{
    private readonly IVanillaSerializer _serializer =
        new VanillaMarkdownSerializer(NullLogger<VanillaMarkdownSerializer>.Instance);

    [Fact]
    public void Serialize_SimpleWorkbook_ProducesExpectedFormat()
    {
        // Arrange
        var context = CreateSimpleWorkbookContext();

        // Act
        var result = _serializer.Serialize(context);
        var resultString = result.ToString();

        // Assert
        Assert.NotEmpty(resultString);
        Assert.Contains("## Sheet: TestSheet", resultString);
        Assert.Contains("A1, Header1", resultString);
        Assert.Contains("B1, Header2", resultString);
        output.WriteLine(resultString);
    }

    [Fact]
    public void Serialize_WithNumberFormats_IncludesFormatStrings()
    {
        // Arrange
        var context = CreateWorkbookWithFormats();
        var options = new VanillaSerializationOptions
        {
            IncludeNumberFormats = true
        };

        // Act
        var result = _serializer.Serialize(context, options);
        var resultString = result.ToString();

        // Assert
        Assert.Contains("#,##0.00", resultString);
        Assert.Contains("dd/mm/yyyy", resultString);
        output.WriteLine(resultString);
    }

    [Fact]
    public void Serialize_ExcludeEmptyCells_OmitsEmptyCells()
    {
        // Arrange
        var context = CreateWorkbookWithEmptyCells();
        var options = new VanillaSerializationOptions
        {
            IncludeEmptyCells = false
        };

        // Act
        var result = _serializer.Serialize(context, options);
        var resultString = result.ToString();

        // Assert
        Assert.DoesNotContain("(empty)", resultString);
        output.WriteLine($"Serialized without empty cells:\n{resultString}");
    }

    [Fact]
    public void Serialize_WithFormulas_IncludesFormulas()
    {
        // Arrange
        var context = CreateWorkbookWithFormulas();
        var options = new VanillaSerializationOptions
        {
            IncludeFormulas = true
        };

        // Act
        var result = _serializer.Serialize(context, options);
        var resultString = result.ToString();

        // Assert
        Assert.Contains("=SUM(A1:A10)", resultString);
        output.WriteLine(resultString);
    }

    [Fact]
    public void EstimateTokenCount_ReturnsReasonableEstimate()
    {
        // Arrange
        var context = CreateLargeWorkbookContext(1000);

        // Act
        var tokenCount = _serializer.EstimateTokenCount(context);

        // Assert
        Assert.True(tokenCount > 0);
        Assert.True(tokenCount < 10000); // Reasonable upper bound for 1000 cells
        output.WriteLine($"Estimated tokens for 1000 cells: {tokenCount}");
    }

    [Fact]
    public void Serialize_WithMaxCells_TruncatesOutput()
    {
        // Arrange
        var context = CreateLargeWorkbookContext(100);
        var options = new VanillaSerializationOptions
        {
            MaxCells = 50
        };

        // Act
        var result = _serializer.Serialize(context, options);
        var resultString = result.ToString();

        // Assert
        Assert.Contains("truncated at 50 cells", resultString);
        output.WriteLine(resultString);
    }

    [Fact]
    public void SerializeWorksheet_SingleSheet_ProducesCorrectOutput()
    {
        // Arrange
        var worksheet = CreateTestWorksheet();

        // Act
        var result = _serializer.SerializeWorksheet(worksheet);
        var resultString = result.ToString();

        // Assert
        Assert.NotEmpty(resultString);
        Assert.DoesNotContain("## Sheet:", resultString); // Should not have sheet header
        output.WriteLine(resultString);
    }

    [Fact]
    public void Serialize_Performance_HandlesLargeWorkbook()
    {
        // Arrange
        var cellCount = 10000;
        var context = CreateLargeWorkbookContext(cellCount);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = _serializer.Serialize(context);
        sw.Stop();

        // Assert
        var resultString = result.ToString();
        Assert.NotEmpty(resultString);
        output.WriteLine($"Serialized {cellCount} cells in {sw.ElapsedMilliseconds}ms");
        output.WriteLine($"Output size: {resultString.Length} characters");
        output.WriteLine($"Performance: {cellCount / (sw.ElapsedMilliseconds / 1000.0):N0} cells/second");
    }

    private WorkbookContext CreateSimpleWorkbookContext()
    {
        var worksheet = new WorksheetContext
        {
            Name = "TestSheet",
            Index = 0,
            Cells = new Dictionary<string, EnhancedCellEntity>
            {
                ["A1"] = new()
                {
                    Address = "A1", RowIndex = 0, ColumnIndex = 0, Value = "Header1", FormattedValue = "Header1",
                    DataType = CellDataType.String
                },
                ["B1"] = new()
                {
                    Address = "B1", RowIndex = 0, ColumnIndex = 1, Value = "Header2", FormattedValue = "Header2",
                    DataType = CellDataType.String
                },
                ["A2"] = new()
                {
                    Address = "A2", RowIndex = 1, ColumnIndex = 0, Value = 100, FormattedValue = "100",
                    DataType = CellDataType.Number
                },
                ["B2"] = new()
                {
                    Address = "B2", RowIndex = 1, ColumnIndex = 1, Value = 200, FormattedValue = "200",
                    DataType = CellDataType.Number
                }
            },
            Dimensions = new WorksheetDimensions
            {
                FirstRowIndex = 0,
                LastRowIndex = 1,
                FirstColumnIndex = 0,
                LastColumnIndex = 1,
                TotalCells = 4,
                NonEmptyCells = 4
            }
        };

        return new WorkbookContext
        {
            FilePath = "test.xlsx",
            Worksheets = new List<WorksheetContext> { worksheet },
            Metadata = new WorkbookMetadata { FileName = "test.xlsx" },
            Statistics = new WorkbookStatistics { TotalCells = 4, NonEmptyCells = 4 }
        };
    }

    private WorkbookContext CreateWorkbookWithFormats()
    {
        var worksheet = new WorksheetContext
        {
            Name = "FormattedSheet",
            Index = 0,
            Cells = new Dictionary<string, EnhancedCellEntity>
            {
                ["A1"] = new()
                {
                    Address = "A1",
                    RowIndex = 0,
                    ColumnIndex = 0,
                    Value = 1234.56,
                    FormattedValue = "1,234.56",
                    NumberFormatString = "#,##0.00",
                    DataType = CellDataType.Number
                },
                ["B1"] = new()
                {
                    Address = "B1",
                    RowIndex = 0,
                    ColumnIndex = 1,
                    Value = DateTime.Now,
                    FormattedValue = DateTime.Now.ToString("dd/MM/yyyy"),
                    NumberFormatString = "dd/mm/yyyy",
                    DataType = CellDataType.DateTime
                }
            },
            Dimensions = new WorksheetDimensions { TotalCells = 2, NonEmptyCells = 2 }
        };

        return new WorkbookContext
        {
            FilePath = "formatted.xlsx",
            Worksheets = new List<WorksheetContext> { worksheet },
            Metadata = new WorkbookMetadata { FileName = "formatted.xlsx" },
            Statistics = new WorkbookStatistics { TotalCells = 2, NonEmptyCells = 2 }
        };
    }

    private WorkbookContext CreateWorkbookWithEmptyCells()
    {
        var cells = new Dictionary<string, EnhancedCellEntity>();

        // Create a sparse matrix
        cells["A1"] = new()
        {
            Address = "A1", RowIndex = 0, ColumnIndex = 0, Value = "Data", FormattedValue = "Data",
            DataType = CellDataType.String
        };
        cells["A2"] = new()
        {
            Address = "A2", RowIndex = 1, ColumnIndex = 0, Value = null, FormattedValue = "",
            DataType = CellDataType.Empty
        };
        cells["A3"] = new()
        {
            Address = "A3", RowIndex = 2, ColumnIndex = 0, Value = "More Data", FormattedValue = "More Data",
            DataType = CellDataType.String
        };

        var worksheet = new WorksheetContext
        {
            Name = "SparseSheet",
            Index = 0,
            Cells = cells,
            Dimensions = new WorksheetDimensions { TotalCells = 3, NonEmptyCells = 2 }
        };

        return new WorkbookContext
        {
            FilePath = "sparse.xlsx",
            Worksheets = new List<WorksheetContext> { worksheet },
            Metadata = new WorkbookMetadata { FileName = "sparse.xlsx" },
            Statistics = new WorkbookStatistics { TotalCells = 3, NonEmptyCells = 2 }
        };
    }

    private WorkbookContext CreateWorkbookWithFormulas()
    {
        var worksheet = new WorksheetContext
        {
            Name = "FormulaSheet",
            Index = 0,
            Cells = new Dictionary<string, EnhancedCellEntity>
            {
                ["A11"] = new()
                {
                    Address = "A11",
                    RowIndex = 10,
                    ColumnIndex = 0,
                    Value = 550,
                    FormattedValue = "550",
                    Formula = "=SUM(A1:A10)",
                    DataType = CellDataType.Formula
                }
            },
            Dimensions = new WorksheetDimensions { TotalCells = 1, NonEmptyCells = 1 }
        };

        return new WorkbookContext
        {
            FilePath = "formulas.xlsx",
            Worksheets = new List<WorksheetContext> { worksheet },
            Metadata = new WorkbookMetadata { FileName = "formulas.xlsx" },
            Statistics = new WorkbookStatistics { TotalCells = 1, NonEmptyCells = 1 }
        };
    }

    private WorkbookContext CreateLargeWorkbookContext(int cellCount)
    {
        var cells = new Dictionary<string, EnhancedCellEntity>();
        var rows = (int)Math.Sqrt(cellCount);
        var cols = cellCount / rows;

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols && cells.Count < cellCount; col++)
            {
                var address = $"{(char)('A' + col)}{row + 1}";
                cells[address] = new EnhancedCellEntity
                {
                    Address = address,
                    RowIndex = row,
                    ColumnIndex = col,
                    Value = $"R{row}C{col}",
                    FormattedValue = $"R{row}C{col}",
                    DataType = CellDataType.String
                };
            }
        }

        var worksheet = new WorksheetContext
        {
            Name = "LargeSheet",
            Index = 0,
            Cells = cells,
            Dimensions = new WorksheetDimensions
            {
                TotalCells = cellCount,
                NonEmptyCells = cellCount,
                LastRowIndex = rows - 1,
                LastColumnIndex = cols - 1
            }
        };

        return new WorkbookContext
        {
            FilePath = "large.xlsx",
            Worksheets = new List<WorksheetContext> { worksheet },
            Metadata = new WorkbookMetadata { FileName = "large.xlsx" },
            Statistics = new WorkbookStatistics
            {
                TotalCells = cellCount,
                NonEmptyCells = cellCount,
                DataTypeDistribution = new Dictionary<CellDataType, int> { [CellDataType.String] = cellCount }
            }
        };
    }

    private WorksheetContext CreateTestWorksheet()
    {
        return new WorksheetContext
        {
            Name = "SingleSheet",
            Index = 0,
            Cells = new Dictionary<string, EnhancedCellEntity>
            {
                ["A1"] = new()
                {
                    Address = "A1", RowIndex = 0, ColumnIndex = 0, Value = "Test", FormattedValue = "Test",
                    DataType = CellDataType.String
                }
            },
            Dimensions = new WorksheetDimensions { TotalCells = 1, NonEmptyCells = 1 }
        };
    }
}