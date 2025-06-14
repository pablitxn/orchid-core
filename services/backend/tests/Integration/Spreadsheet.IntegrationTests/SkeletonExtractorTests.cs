using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Spreadsheet.IntegrationTests;

public class SkeletonExtractorTests
{
    private readonly ITestOutputHelper _output;
    private readonly ISkeletonExtractor _extractor;
    private readonly IStructuralAnchorDetector _anchorDetector;

    public SkeletonExtractorTests(ITestOutputHelper output)
    {
        _output = output;
        _extractor = new SkeletonExtractor(NullLogger<SkeletonExtractor>.Instance);
        _anchorDetector = new StructuralAnchorDetector(NullLogger<StructuralAnchorDetector>.Instance);
    }

    [Fact]
    public async Task ExtractSkeletonAsync_SimpleWorksheet_CompressesCorrectly()
    {
        // Arrange
        var worksheet = CreateLargeWorksheet(100, 20); // 2000 cells
        var anchors = await _anchorDetector.FindAnchorsAsync(worksheet, k: 1);

        // Act
        var skeleton = await _extractor.ExtractSkeletonAsync(worksheet, anchors);

        // Assert
        Assert.NotNull(skeleton);
        Assert.Equal(worksheet.Name, skeleton.OriginalName);
        Assert.True(skeleton.Stats.CompressionRatio >= 0.7);
        Assert.True(skeleton.SkeletonCells.Count < worksheet.Cells.Count);
        
        _output.WriteLine($"Original cells: {skeleton.Stats.OriginalCellCount}");
        _output.WriteLine($"Skeleton cells: {skeleton.Stats.SkeletonCellCount}");
        _output.WriteLine($"Compression ratio: {skeleton.Stats.CompressionRatio:P}");
    }

    [Fact]
    public async Task ExtractSkeletonAsync_PreservesAnchors()
    {
        // Arrange
        var worksheet = CreateWorksheetWithKnownAnchors();
        var anchors = new StructuralAnchors
        {
            AnchorRows = new HashSet<int> { 0, 5, 10 }, // Header and specific data rows
            AnchorColumns = new HashSet<int> { 0, 2 }, // First and third columns
            HeaderRegions = new List<HeaderRegion>
            {
                new() { StartRow = 0, EndRow = 0, StartColumn = 0, EndColumn = 3, Type = HeaderType.Simple }
            }
        };

        // Act
        var skeleton = await _extractor.ExtractSkeletonAsync(worksheet, anchors);

        // Assert
        // Check that anchor cells are preserved
        foreach (var row in anchors.AnchorRows)
        {
            foreach (var col in anchors.AnchorColumns)
            {
                var originalAddress = $"{(char)('A' + col)}{row + 1}";
                if (worksheet.Cells.TryGetValue(originalAddress, out var originalCell))
                {
                    // Find the remapped cell
                    var preserved = skeleton.SkeletonCells.Values
                        .Any(c => skeleton.AddressMapping.SkeletonToOriginal[c.Address] == originalAddress);
                    Assert.True(preserved, $"Anchor cell {originalAddress} should be preserved");
                }
            }
        }
    }

    [Fact]
    public async Task ExtractSkeletonAsync_AddressMappingIsConsistent()
    {
        // Arrange
        var worksheet = CreateLargeWorksheet(50, 10);
        var anchors = await _anchorDetector.FindAnchorsAsync(worksheet, k: 2);

        // Act
        var skeleton = await _extractor.ExtractSkeletonAsync(worksheet, anchors);

        // Assert
        // Verify bidirectional mapping
        foreach (var (skeletonAddr, originalAddr) in skeleton.AddressMapping.SkeletonToOriginal)
        {
            Assert.Contains(originalAddr, skeleton.AddressMapping.OriginalToSkeleton.Keys);
            Assert.Equal(skeletonAddr, skeleton.AddressMapping.OriginalToSkeleton[originalAddr]);
        }

        // Verify all skeleton cells have mappings
        foreach (var skeletonCell in skeleton.SkeletonCells.Values)
        {
            Assert.Contains(skeletonCell.Address, skeleton.AddressMapping.SkeletonToOriginal.Keys);
        }
    }

    [Fact]
    public async Task ExtractSkeletonAsync_PreservesFormulas()
    {
        // Arrange
        var worksheet = CreateWorksheetWithFormulas();
        var anchors = await _anchorDetector.FindAnchorsAsync(worksheet);
        var options = new SkeletonExtractionOptions { PreserveFormulas = true };

        // Act
        var skeleton = await _extractor.ExtractSkeletonAsync(worksheet, anchors, options);

        // Assert
        var formulaCells = worksheet.Cells.Values.Where(c => c.DataType == CellDataType.Formula).ToList();
        foreach (var formulaCell in formulaCells)
        {
            var preserved = skeleton.AddressMapping.OriginalToSkeleton.ContainsKey(formulaCell.Address);
            Assert.True(preserved, $"Formula cell {formulaCell.Address} should be preserved");
        }
    }

    [Fact]
    public async Task ExtractSkeletonAsync_PreservesSpecialFormatting()
    {
        // Arrange
        var worksheet = CreateWorksheetWithFormatting();
        var anchors = await _anchorDetector.FindAnchorsAsync(worksheet);
        var options = new SkeletonExtractionOptions { PreserveFormattedCells = true };

        // Act
        var skeleton = await _extractor.ExtractSkeletonAsync(worksheet, anchors, options);

        // Assert
        var formattedCells = worksheet.Cells.Values
            .Where(c => c.Style != null && (c.Style.IsBold || c.Style.HasBorders || !string.IsNullOrEmpty(c.Style.BackgroundColor)))
            .ToList();
            
        foreach (var formattedCell in formattedCells)
        {
            var preserved = skeleton.AddressMapping.OriginalToSkeleton.ContainsKey(formattedCell.Address);
            Assert.True(preserved, $"Formatted cell {formattedCell.Address} should be preserved");
        }
    }

    [Fact]
    public async Task ExtractWorkbookSkeletonAsync_MultipleSheets()
    {
        // Arrange
        var workbook = CreateMultiSheetWorkbook();
        var anchors = await _anchorDetector.FindWorkbookAnchorsAsync(workbook);

        // Act
        var skeleton = await _extractor.ExtractWorkbookSkeletonAsync(workbook, anchors);

        // Assert
        Assert.NotNull(skeleton);
        Assert.Equal(workbook.FilePath, skeleton.OriginalFilePath);
        Assert.Equal(workbook.Worksheets.Count, skeleton.Sheets.Count);
        Assert.True(skeleton.GlobalStats.CompressionRatio > 0);
        
        _output.WriteLine($"Global compression: {skeleton.GlobalStats.CompressionRatio:P}");
        _output.WriteLine($"Total cells: {skeleton.GlobalStats.OriginalCellCount} → {skeleton.GlobalStats.SkeletonCellCount}");
    }

    [Fact]
    public async Task ExtractSkeletonAsync_MinCompressionRatio_Warning()
    {
        // Arrange
        var worksheet = CreateDenseWorksheet(); // Mostly anchor cells
        var anchors = new StructuralAnchors
        {
            AnchorRows = new HashSet<int>(Enumerable.Range(0, 10)), // All rows are anchors
            AnchorColumns = new HashSet<int>(Enumerable.Range(0, 5)) // All columns are anchors
        };
        var options = new SkeletonExtractionOptions { MinCompressionRatio = 0.9 };

        // Act
        var skeleton = await _extractor.ExtractSkeletonAsync(worksheet, anchors, options);

        // Assert
        Assert.True(skeleton.Stats.CompressionRatio < options.MinCompressionRatio);
        _output.WriteLine($"Warning: Compression ratio {skeleton.Stats.CompressionRatio:P} below target {options.MinCompressionRatio:P}");
    }

    [Fact]
    public async Task ExtractSkeletonAsync_EmptyWorksheet_HandlesGracefully()
    {
        // Arrange
        var worksheet = new WorksheetContext
        {
            Name = "Empty",
            Cells = new Dictionary<string, EnhancedCellEntity>(),
            Dimensions = new WorksheetDimensions()
        };
        var anchors = new StructuralAnchors();

        // Act
        var skeleton = await _extractor.ExtractSkeletonAsync(worksheet, anchors);

        // Assert
        Assert.Empty(skeleton.SkeletonCells);
        Assert.Equal(0, skeleton.Stats.OriginalCellCount);
        Assert.Equal(0, skeleton.Stats.SkeletonCellCount);
    }

    private WorksheetContext CreateLargeWorksheet(int rows, int cols)
    {
        var cells = new Dictionary<string, EnhancedCellEntity>();
        
        // Add headers
        for (var col = 0; col < cols; col++)
        {
            var address = $"{(char)('A' + col)}1";
            cells[address] = new EnhancedCellEntity
            {
                Address = address,
                RowIndex = 0,
                ColumnIndex = col,
                Value = $"Header{col + 1}",
                FormattedValue = $"Header{col + 1}",
                DataType = CellDataType.String,
                Style = new CellStyleMetadata { IsBold = true }
            };
        }

        // Add data with some patterns
        for (var row = 1; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var address = $"{GetColumnName(col)}{row + 1}";
                
                // Create different data patterns
                var patternIndex = (row + col) % 3;
                var dataType = patternIndex switch
                {
                    0 => CellDataType.Number,
                    1 => CellDataType.String,
                    _ => CellDataType.Empty
                };

                if (dataType != CellDataType.Empty)
                {
                    cells[address] = new EnhancedCellEntity
                    {
                        Address = address,
                        RowIndex = row,
                        ColumnIndex = col,
                        Value = dataType == CellDataType.Number ? (object)(row * col) : $"Data{row},{col}",
                        FormattedValue = dataType == CellDataType.Number ? (row * col).ToString() : $"Data{row},{col}",
                        DataType = dataType
                    };
                }
            }
        }

        return new WorksheetContext
        {
            Name = "LargeSheet",
            Index = 0,
            Cells = cells,
            Dimensions = new WorksheetDimensions
            {
                FirstRowIndex = 0,
                LastRowIndex = rows - 1,
                FirstColumnIndex = 0,
                LastColumnIndex = cols - 1,
                TotalCells = rows * cols,
                NonEmptyCells = cells.Count
            }
        };
    }

    private WorksheetContext CreateWorksheetWithKnownAnchors()
    {
        var cells = new Dictionary<string, EnhancedCellEntity>();
        
        // Headers (row 0)
        for (var col = 0; col < 4; col++)
        {
            var address = $"{(char)('A' + col)}1";
            cells[address] = new EnhancedCellEntity
            {
                Address = address,
                RowIndex = 0,
                ColumnIndex = col,
                Value = $"Header{col}",
                FormattedValue = $"Header{col}",
                DataType = CellDataType.String,
                Style = new CellStyleMetadata { IsBold = true }
            };
        }

        // Data rows with some anchor rows having special formatting
        for (var row = 1; row <= 15; row++)
        {
            for (var col = 0; col < 4; col++)
            {
                var address = $"{(char)('A' + col)}{row + 1}";
                var isAnchorRow = row == 5 || row == 10;
                
                cells[address] = new EnhancedCellEntity
                {
                    Address = address,
                    RowIndex = row,
                    ColumnIndex = col,
                    Value = $"R{row}C{col}",
                    FormattedValue = $"R{row}C{col}",
                    DataType = CellDataType.String,
                    Style = isAnchorRow ? new CellStyleMetadata { BackgroundColor = "#FFFF00" } : null
                };
            }
        }

        return new WorksheetContext
        {
            Name = "KnownAnchors",
            Index = 0,
            Cells = cells,
            Dimensions = new WorksheetDimensions
            {
                FirstRowIndex = 0,
                LastRowIndex = 15,
                FirstColumnIndex = 0,
                LastColumnIndex = 3,
                TotalCells = cells.Count,
                NonEmptyCells = cells.Count
            }
        };
    }

    private WorksheetContext CreateWorksheetWithFormulas()
    {
        var cells = new Dictionary<string, EnhancedCellEntity>();
        
        // Data cells
        for (var row = 0; row < 10; row++)
        {
            cells[$"A{row + 1}"] = new EnhancedCellEntity
            {
                Address = $"A{row + 1}",
                RowIndex = row,
                ColumnIndex = 0,
                Value = (row + 1) * 10,
                FormattedValue = ((row + 1) * 10).ToString(),
                DataType = CellDataType.Number
            };
        }

        // Formula cells
        cells["A11"] = new EnhancedCellEntity
        {
            Address = "A11",
            RowIndex = 10,
            ColumnIndex = 0,
            Value = 550,
            FormattedValue = "550",
            Formula = "=SUM(A1:A10)",
            DataType = CellDataType.Formula
        };

        cells["B11"] = new EnhancedCellEntity
        {
            Address = "B11",
            RowIndex = 10,
            ColumnIndex = 1,
            Value = 55,
            FormattedValue = "55",
            Formula = "=AVERAGE(A1:A10)",
            DataType = CellDataType.Formula
        };

        return new WorksheetContext
        {
            Name = "FormulaSheet",
            Index = 0,
            Cells = cells,
            Dimensions = new WorksheetDimensions
            {
                FirstRowIndex = 0,
                LastRowIndex = 10,
                FirstColumnIndex = 0,
                LastColumnIndex = 1,
                TotalCells = cells.Count,
                NonEmptyCells = cells.Count
            }
        };
    }

    private WorksheetContext CreateWorksheetWithFormatting()
    {
        var cells = new Dictionary<string, EnhancedCellEntity>();
        
        // Various formatting styles
        cells["A1"] = new EnhancedCellEntity
        {
            Address = "A1",
            RowIndex = 0,
            ColumnIndex = 0,
            Value = "Bold Text",
            FormattedValue = "Bold Text",
            DataType = CellDataType.String,
            Style = new CellStyleMetadata { IsBold = true }
        };

        cells["A2"] = new EnhancedCellEntity
        {
            Address = "A2",
            RowIndex = 1,
            ColumnIndex = 0,
            Value = "Colored Cell",
            FormattedValue = "Colored Cell",
            DataType = CellDataType.String,
            Style = new CellStyleMetadata { BackgroundColor = "#FF0000" }
        };

        cells["A3"] = new EnhancedCellEntity
        {
            Address = "A3",
            RowIndex = 2,
            ColumnIndex = 0,
            Value = "Bordered Cell",
            FormattedValue = "Bordered Cell",
            DataType = CellDataType.String,
            Style = new CellStyleMetadata { HasBorders = true }
        };

        cells["A4"] = new EnhancedCellEntity
        {
            Address = "A4",
            RowIndex = 3,
            ColumnIndex = 0,
            Value = "Normal Cell",
            FormattedValue = "Normal Cell",
            DataType = CellDataType.String
        };

        return new WorksheetContext
        {
            Name = "FormattedSheet",
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

    private WorksheetContext CreateDenseWorksheet()
    {
        var cells = new Dictionary<string, EnhancedCellEntity>();
        
        // Create a small dense worksheet where most cells would be preserved
        for (var row = 0; row < 10; row++)
        {
            for (var col = 0; col < 5; col++)
            {
                var address = $"{(char)('A' + col)}{row + 1}";
                cells[address] = new EnhancedCellEntity
                {
                    Address = address,
                    RowIndex = row,
                    ColumnIndex = col,
                    Value = $"Important{row},{col}",
                    FormattedValue = $"Important{row},{col}",
                    DataType = CellDataType.String,
                    Style = new CellStyleMetadata { IsBold = true } // All cells are "important"
                };
            }
        }

        return new WorksheetContext
        {
            Name = "DenseSheet",
            Index = 0,
            Cells = cells,
            Dimensions = new WorksheetDimensions
            {
                FirstRowIndex = 0,
                LastRowIndex = 9,
                FirstColumnIndex = 0,
                LastColumnIndex = 4,
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
                CreateLargeWorksheet(50, 10),
                CreateWorksheetWithFormulas(),
                CreateWorksheetWithFormatting()
            },
            Metadata = new WorkbookMetadata { FileName = "multisheet.xlsx" },
            Statistics = new WorkbookStatistics()
        };
    }

    private string GetColumnName(int columnIndex)
    {
        var columnName = "";
        while (columnIndex >= 0)
        {
            columnName = (char)('A' + columnIndex % 26) + columnName;
            columnIndex = columnIndex / 26 - 1;
        }
        return columnName;
    }
}