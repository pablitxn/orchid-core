using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Spreadsheet.IntegrationTests;

public class StructuralAnchorDetectorTests
{
    private readonly ITestOutputHelper _output;
    private readonly IStructuralAnchorDetector _detector;

    public StructuralAnchorDetectorTests(ITestOutputHelper output)
    {
        _output = output;
        _detector = new StructuralAnchorDetector(NullLogger<StructuralAnchorDetector>.Instance);
    }

    [Fact]
    public async Task FindAnchorsAsync_SimpleHeaderRow_DetectsHeader()
    {
        // Arrange
        var worksheet = CreateWorksheetWithHeaders();

        // Act
        var result = await _detector.FindAnchorsAsync(worksheet);

        // Assert
        Assert.NotEmpty(result.AnchorRows);
        Assert.Contains(0, result.AnchorRows); // Header row
        Assert.NotEmpty(result.HeaderRegions);
        Assert.Equal(HeaderType.Simple, result.HeaderRegions.First().Type);
        _output.WriteLine($"Detected {result.AnchorRows.Count} anchor rows");
    }

    [Fact]
    public async Task FindAnchorsAsync_HeterogeneousData_DetectsAnchors()
    {
        // Arrange
        var worksheet = CreateHeterogeneousWorksheet();

        // Act
        var result = await _detector.FindAnchorsAsync(worksheet, k: 1);

        // Assert
        Assert.NotEmpty(result.AnchorRows);
        Assert.NotEmpty(result.AnchorColumns);
        Assert.True(result.Metrics.TotalAnchors > 0);
        _output.WriteLine($"Total anchors: {result.Metrics.TotalAnchors}");
        _output.WriteLine($"Anchor density: {result.Metrics.AnchorDensity:P}");
    }

    [Fact]
    public async Task FindAnchorsAsync_WithKParameter_ExpandsAnchors()
    {
        // Arrange
        var worksheet = CreateWorksheetWithHeaders();
        
        // Act
        var resultK0 = await _detector.FindAnchorsAsync(worksheet, k: 0);
        var resultK2 = await _detector.FindAnchorsAsync(worksheet, k: 2);

        // Assert
        Assert.True(resultK2.AnchorRows.Count >= resultK0.AnchorRows.Count);
        _output.WriteLine($"Anchors with k=0: {resultK0.AnchorRows.Count}");
        _output.WriteLine($"Anchors with k=2: {resultK2.AnchorRows.Count}");
    }

    [Fact]
    public async Task FindAnchorsAsync_MultiLevelHeaders_DetectsCorrectly()
    {
        // Arrange
        var worksheet = CreateMultiLevelHeaderWorksheet();
        var options = new AnchorDetectionOptions
        {
            DetectMultiLevelHeaders = true,
            MaxHeaderDepth = 3
        };

        // Act
        var result = await _detector.FindAnchorsAsync(worksheet, k: 1, options);

        // Assert
        Assert.NotEmpty(result.HeaderRegions);
        var header = result.HeaderRegions.First();
        Assert.Equal(HeaderType.MultiLevel, header.Type);
        Assert.True(header.EndRow > header.StartRow);
        _output.WriteLine($"Detected multi-level header from row {header.StartRow} to {header.EndRow}");
    }

    [Fact]
    public async Task FindAnchorsAsync_StyleChanges_DetectsWhenEnabled()
    {
        // Arrange
        var worksheet = CreateWorksheetWithStyles();
        var optionsWithStyle = new AnchorDetectionOptions { ConsiderStyles = true };
        var optionsWithoutStyle = new AnchorDetectionOptions { ConsiderStyles = false };

        // Act
        var resultWith = await _detector.FindAnchorsAsync(worksheet, k: 0, optionsWithStyle);
        var resultWithout = await _detector.FindAnchorsAsync(worksheet, k: 0, optionsWithoutStyle);

        // Assert
        Assert.True(resultWith.AnchorRows.Count >= resultWithout.AnchorRows.Count);
        _output.WriteLine($"Anchors with style consideration: {resultWith.AnchorRows.Count}");
        _output.WriteLine($"Anchors without style consideration: {resultWithout.AnchorRows.Count}");
    }

    [Fact]
    public async Task FindWorkbookAnchorsAsync_MultipleSheets_DetectsPatterns()
    {
        // Arrange
        var workbook = CreateMultiSheetWorkbook();

        // Act
        var result = await _detector.FindWorkbookAnchorsAsync(workbook);

        // Assert
        Assert.NotEmpty(result.WorksheetAnchors);
        Assert.Equal(workbook.Worksheets.Count, result.WorksheetAnchors.Count);
        
        if (result.CrossSheetPatterns.Any())
        {
            _output.WriteLine($"Detected {result.CrossSheetPatterns.Count} cross-sheet patterns");
            foreach (var pattern in result.CrossSheetPatterns)
            {
                _output.WriteLine($"Pattern: {pattern.PatternName} affects {pattern.AffectedSheets.Count} sheets");
            }
        }
    }

    [Fact]
    public async Task FindAnchorsAsync_EmptyWorksheet_ReturnsEmptyAnchors()
    {
        // Arrange
        var worksheet = new WorksheetContext
        {
            Name = "Empty",
            Index = 0,
            Cells = new Dictionary<string, EnhancedCellEntity>(),
            Dimensions = new WorksheetDimensions()
        };

        // Act
        var result = await _detector.FindAnchorsAsync(worksheet);

        // Assert
        Assert.Empty(result.AnchorRows);
        Assert.Empty(result.AnchorColumns);
        Assert.Empty(result.HeaderRegions);
    }

    [Fact]
    public async Task FindAnchorsAsync_CalculatesMetricsCorrectly()
    {
        // Arrange
        var worksheet = CreateHeterogeneousWorksheet();

        // Act
        var result = await _detector.FindAnchorsAsync(worksheet);

        // Assert
        var metrics = result.Metrics;
        Assert.True(metrics.TotalAnchors >= 0);
        Assert.InRange(metrics.AnchorDensity, 0, 1);
        Assert.NotEmpty(metrics.HeterogeneityScores);
        Assert.Contains("RowAverage", metrics.HeterogeneityScores.Keys);
        Assert.Contains("ColumnAverage", metrics.HeterogeneityScores.Keys);
        
        _output.WriteLine($"Heterogeneity scores: {string.Join(", ", metrics.HeterogeneityScores.Select(kvp => $"{kvp.Key}={kvp.Value:F2}"))}");
    }

    private WorksheetContext CreateWorksheetWithHeaders()
    {
        var cells = new Dictionary<string, EnhancedCellEntity>();
        
        // Header row (bold text)
        var headers = new[] { "ID", "Name", "Amount", "Date" };
        for (var col = 0; col < headers.Length; col++)
        {
            var address = $"{(char)('A' + col)}1";
            cells[address] = new EnhancedCellEntity
            {
                Address = address,
                RowIndex = 0,
                ColumnIndex = col,
                Value = headers[col],
                FormattedValue = headers[col],
                DataType = CellDataType.String,
                Style = new CellStyleMetadata { IsBold = true }
            };
        }

        // Data rows
        for (var row = 1; row <= 10; row++)
        {
            cells[$"A{row + 1}"] = new() { Address = $"A{row + 1}", RowIndex = row, ColumnIndex = 0, Value = row, FormattedValue = row.ToString(), DataType = CellDataType.Number };
            cells[$"B{row + 1}"] = new() { Address = $"B{row + 1}", RowIndex = row, ColumnIndex = 1, Value = $"Item {row}", FormattedValue = $"Item {row}", DataType = CellDataType.String };
            cells[$"C{row + 1}"] = new() { Address = $"C{row + 1}", RowIndex = row, ColumnIndex = 2, Value = row * 100.0, FormattedValue = (row * 100.0).ToString(), DataType = CellDataType.Number };
            cells[$"D{row + 1}"] = new() { Address = $"D{row + 1}", RowIndex = row, ColumnIndex = 3, Value = DateTime.Now, FormattedValue = DateTime.Now.ToShortDateString(), DataType = CellDataType.DateTime };
        }

        return new WorksheetContext
        {
            Name = "DataSheet",
            Index = 0,
            Cells = cells,
            Dimensions = new WorksheetDimensions 
            { 
                FirstRowIndex = 0,
                LastRowIndex = 10,
                FirstColumnIndex = 0,
                LastColumnIndex = 3,
                TotalCells = cells.Count, 
                NonEmptyCells = cells.Count 
            }
        };
    }

    private WorksheetContext CreateHeterogeneousWorksheet()
    {
        var cells = new Dictionary<string, EnhancedCellEntity>();
        
        // Mixed data types in rows and columns
        cells["A1"] = new() { Address = "A1", RowIndex = 0, ColumnIndex = 0, Value = "Category", FormattedValue = "Category", DataType = CellDataType.String, Style = new() { IsBold = true } };
        cells["B1"] = new() { Address = "B1", RowIndex = 0, ColumnIndex = 1, Value = 2023, FormattedValue = "2023", DataType = CellDataType.Number };
        cells["C1"] = new() { Address = "C1", RowIndex = 0, ColumnIndex = 2, Value = DateTime.Now, FormattedValue = DateTime.Now.ToString(), DataType = CellDataType.DateTime };
        
        cells["A2"] = new() { Address = "A2", RowIndex = 1, ColumnIndex = 0, Value = "Sales", FormattedValue = "Sales", DataType = CellDataType.String };
        cells["B2"] = new() { Address = "B2", RowIndex = 1, ColumnIndex = 1, Value = 1000000, FormattedValue = "1,000,000", DataType = CellDataType.Number, NumberFormatString = "#,##0" };
        cells["C2"] = new() { Address = "C2", RowIndex = 1, ColumnIndex = 2, Value = true, FormattedValue = "TRUE", DataType = CellDataType.Boolean };

        // Separator row with different style
        cells["A3"] = new() { Address = "A3", RowIndex = 2, ColumnIndex = 0, Value = "---", FormattedValue = "---", DataType = CellDataType.String, Style = new() { BackgroundColor = "#CCCCCC" } };
        cells["B3"] = new() { Address = "B3", RowIndex = 2, ColumnIndex = 1, Value = "---", FormattedValue = "---", DataType = CellDataType.String, Style = new() { BackgroundColor = "#CCCCCC" } };
        cells["C3"] = new() { Address = "C3", RowIndex = 2, ColumnIndex = 2, Value = "---", FormattedValue = "---", DataType = CellDataType.String, Style = new() { BackgroundColor = "#CCCCCC" } };

        return new WorksheetContext
        {
            Name = "HeterogeneousSheet",
            Index = 0,
            Cells = cells,
            Dimensions = new WorksheetDimensions 
            { 
                FirstRowIndex = 0,
                LastRowIndex = 2,
                FirstColumnIndex = 0,
                LastColumnIndex = 2,
                TotalCells = cells.Count, 
                NonEmptyCells = cells.Count 
            }
        };
    }

    private WorksheetContext CreateMultiLevelHeaderWorksheet()
    {
        var cells = new Dictionary<string, EnhancedCellEntity>();
        
        // Level 1 headers
        cells["A1"] = new() { Address = "A1", RowIndex = 0, ColumnIndex = 0, Value = "Sales Data", FormattedValue = "Sales Data", DataType = CellDataType.String, Style = new() { IsBold = true, IsMerged = true, MergedColumnSpan = 4 } };
        
        // Level 2 headers
        cells["A2"] = new() { Address = "A2", RowIndex = 1, ColumnIndex = 0, Value = "Q1", FormattedValue = "Q1", DataType = CellDataType.String, Style = new() { IsBold = true, IsMerged = true, MergedColumnSpan = 2 } };
        cells["C2"] = new() { Address = "C2", RowIndex = 1, ColumnIndex = 2, Value = "Q2", FormattedValue = "Q2", DataType = CellDataType.String, Style = new() { IsBold = true, IsMerged = true, MergedColumnSpan = 2 } };
        
        // Level 3 headers
        cells["A3"] = new() { Address = "A3", RowIndex = 2, ColumnIndex = 0, Value = "Jan", FormattedValue = "Jan", DataType = CellDataType.String, Style = new() { IsBold = true } };
        cells["B3"] = new() { Address = "B3", RowIndex = 2, ColumnIndex = 1, Value = "Feb", FormattedValue = "Feb", DataType = CellDataType.String, Style = new() { IsBold = true } };
        cells["C3"] = new() { Address = "C3", RowIndex = 2, ColumnIndex = 2, Value = "Mar", FormattedValue = "Mar", DataType = CellDataType.String, Style = new() { IsBold = true } };
        cells["D3"] = new() { Address = "D3", RowIndex = 2, ColumnIndex = 3, Value = "Apr", FormattedValue = "Apr", DataType = CellDataType.String, Style = new() { IsBold = true } };

        return new WorksheetContext
        {
            Name = "MultiLevelHeaders",
            Index = 0,
            Cells = cells,
            Dimensions = new WorksheetDimensions 
            { 
                FirstRowIndex = 0,
                LastRowIndex = 2,
                FirstColumnIndex = 0,
                LastColumnIndex = 3,
                TotalCells = cells.Count, 
                NonEmptyCells = cells.Count 
            }
        };
    }

    private WorksheetContext CreateWorksheetWithStyles()
    {
        var cells = new Dictionary<string, EnhancedCellEntity>();
        
        // Normal row
        cells["A1"] = new() { Address = "A1", RowIndex = 0, ColumnIndex = 0, Value = "Normal", FormattedValue = "Normal", DataType = CellDataType.String };
        
        // Bold row
        cells["A2"] = new() { Address = "A2", RowIndex = 1, ColumnIndex = 0, Value = "Bold", FormattedValue = "Bold", DataType = CellDataType.String, Style = new() { IsBold = true } };
        
        // Colored row
        cells["A3"] = new() { Address = "A3", RowIndex = 2, ColumnIndex = 0, Value = "Colored", FormattedValue = "Colored", DataType = CellDataType.String, Style = new() { BackgroundColor = "#FFFF00" } };
        
        // Bordered row
        cells["A4"] = new() { Address = "A4", RowIndex = 3, ColumnIndex = 0, Value = "Bordered", FormattedValue = "Bordered", DataType = CellDataType.String, Style = new() { HasBorders = true } };

        return new WorksheetContext
        {
            Name = "StyledSheet",
            Index = 0,
            Cells = cells,
            Dimensions = new WorksheetDimensions 
            { 
                FirstRowIndex = 0,
                LastRowIndex = 3,
                FirstColumnIndex = 0,
                LastColumnIndex = 0,
                TotalCells = cells.Count, 
                NonEmptyCells = cells.Count 
            }
        };
    }

    private WorkbookContext CreateMultiSheetWorkbook()
    {
        return new WorkbookContext
        {
            FilePath = "multisheet.xlsx",
            Worksheets = new List<WorksheetContext>
            {
                CreateWorksheetWithHeaders(),
                CreateHeterogeneousWorksheet(),
                CreateMultiLevelHeaderWorksheet()
            },
            Metadata = new WorkbookMetadata { FileName = "multisheet.xlsx" },
            Statistics = new WorkbookStatistics()
        };
    }
}