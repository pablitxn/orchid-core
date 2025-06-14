using Application.UseCases.Spreadsheet.Compression;
using Domain.ValueObjects.Spreadsheet;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Application.Tests.UseCases.Spreadsheet.Compression;

public class CompressionBenchmarkTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    
    public CompressionBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
        
        var services = new ServiceCollection();
        services.AddSpreadsheetCompression();
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();
    }
    
    [Fact]
    public async Task InvertedIndex_AchievesMinimum1_4xCompression_OnTypicalDataset()
    {
        // Arrange
        var workbook = CreateHighlyRepetitiveDataset(1000);
        var pipeline = SpreadsheetCompressionFactory.CreateLightweightPipeline(_serviceProvider);
        
        // Act
        var result = await pipeline.ExecuteAsync(workbook);
        
        // Assert
        _output.WriteLine($"Original tokens: {result.OriginalTokenCount}");
        _output.WriteLine($"Compressed tokens: {result.CompressedTokenCount}");
        _output.WriteLine($"Compression ratio: {result.CompressionRatio:F2}x");
        
        result.CompressionRatio.Should().BeGreaterThanOrEqualTo(1.4);
    }
    
    [Fact]
    public async Task FormatAggregation_AchievesMinimum2_5xCompression_OnStructuredDataset()
    {
        // Arrange
        var workbook = CreateFormatOptimizedDataset(5000);
        var pipeline = SpreadsheetCompressionFactory.CreateStandardPipeline(_serviceProvider);
        
        // Act
        var result = await pipeline.ExecuteAsync(workbook);
        
        // Assert
        _output.WriteLine($"Original tokens: {result.OriginalTokenCount}");
        _output.WriteLine($"Compressed tokens: {result.CompressedTokenCount}");
        _output.WriteLine($"Compression ratio: {result.CompressionRatio:F2}x");
        
        result.CompressionRatio.Should().BeGreaterThanOrEqualTo(2.5);
    }
    
    [Fact]
    public async Task Pipeline_ExecutesUnder10Seconds_For50kCells()
    {
        // Arrange
        var workbook = CreateLargeDataset(50000);
        var pipeline = SpreadsheetCompressionFactory.CreateOptimalPipeline(_serviceProvider, workbook);
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await pipeline.ExecuteAsync(workbook);
        stopwatch.Stop();
        
        // Assert
        _output.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Cells processed: {workbook.Statistics.TotalCells}");
        _output.WriteLine($"Compression ratio: {result.CompressionRatio:F2}x");
        
        foreach (var (stepName, timing) in result.StepTimings)
        {
            _output.WriteLine($"Step {stepName}: {timing.TotalMilliseconds}ms");
        }
        
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        result.CompressionRatio.Should().BeGreaterThan(1.0);
    }
    
    [Theory]
    [InlineData(1000)]
    [InlineData(10000)]
    [InlineData(25000)]
    public async Task OptimalPipeline_SelectsAppropriateStrategy_BasedOnDatasetSize(int cellCount)
    {
        // Arrange
        var workbook = CreateVariedDataset(cellCount);
        
        // Act
        var pipeline = SpreadsheetCompressionFactory.CreateOptimalPipeline(_serviceProvider, workbook);
        var result = await pipeline.ExecuteAsync(workbook);
        
        // Assert
        _output.WriteLine($"Dataset size: {cellCount} cells");
        _output.WriteLine($"Compression ratio: {result.CompressionRatio:F2}x");
        _output.WriteLine($"Steps executed: {string.Join(", ", result.StepTimings.Keys)}");
        
        result.CompressionRatio.Should().BeGreaterThan(1.0);
        result.StepTimings.Should().NotBeEmpty();
    }
    
    [Fact]
    public async Task CompressionResult_IncludesDetailedMetrics()
    {
        // Arrange
        var workbook = CreateTypicalInvoiceDataset(500);
        var pipeline = SpreadsheetCompressionFactory.CreateHighCompressionPipeline(_serviceProvider);
        
        // Act
        var result = await pipeline.ExecuteAsync(workbook);
        
        // Assert
        result.CompressedText.Should().NotBeEmpty();
        result.OriginalTokenCount.Should().BeGreaterThan(0);
        result.CompressedTokenCount.Should().BeGreaterThan(0);
        result.StepTimings.Should().NotBeEmpty();
        result.Artifacts.Should().NotBeEmpty();
        
        _output.WriteLine($"Generated {result.Artifacts.Count} artifacts:");
        foreach (var artifact in result.Artifacts)
        {
            _output.WriteLine($"- {artifact.Name} ({artifact.Type}): {artifact.Data.Length} bytes");
        }
    }
    
    private static WorkbookContext CreateTypicalInvoiceDataset(int rowCount)
    {
        var cells = new List<CellData>
        {
            // Headers
            new() { Address = new CellAddress(0, 0), Value = "Invoice#" },
            new() { Address = new CellAddress(0, 1), Value = "Date" },
            new() { Address = new CellAddress(0, 2), Value = "Customer" },
            new() { Address = new CellAddress(0, 3), Value = "Amount" },
            new() { Address = new CellAddress(0, 4), Value = "Tax" },
            new() { Address = new CellAddress(0, 5), Value = "Total" }
        };
        
        // Data rows
        for (int i = 1; i <= rowCount; i++)
        {
            cells.Add(new CellData { Address = new CellAddress(i, 0), Value = $"INV-{i:D6}" });
            cells.Add(new CellData { Address = new CellAddress(i, 1), Value = $"2024-{(i % 12) + 1:D2}-{(i % 28) + 1:D2}" });
            cells.Add(new CellData { Address = new CellAddress(i, 2), Value = $"Customer {i % 100}" });
            cells.Add(new CellData { Address = new CellAddress(i, 3), Value = 1000 + (i * 123.45m), NumberFormatString = "$#,##0.00" });
            cells.Add(new CellData { Address = new CellAddress(i, 4), Value = (1000 + (i * 123.45m)) * 0.08m, NumberFormatString = "$#,##0.00" });
            cells.Add(new CellData { Address = new CellAddress(i, 5), Value = (1000 + (i * 123.45m)) * 1.08m, NumberFormatString = "$#,##0.00" });
        }
        
        return new WorkbookContext
        {
            Name = "InvoiceData",
            Worksheets = new[]
            {
                new WorksheetContext
                {
                    Name = "Invoices",
                    Cells = cells,
                    Dimensions = (rowCount + 1, 6)
                }
            },
            Statistics = new WorkbookStatistics
            {
                TotalCells = cells.Count,
                EmptyCells = 0,
                TypeDistribution = new Dictionary<Type, int>
                {
                    [typeof(string)] = (rowCount + 1) * 3,
                    [typeof(decimal)] = rowCount * 3
                }
            }
        };
    }
    
    private static WorkbookContext CreateStructuredFinancialDataset(int rowCount)
    {
        var cells = new List<CellData>();
        
        // Create multiple worksheets with different data types
        var sheets = new List<WorksheetContext>();
        
        // Revenue sheet with percentages and currency
        var revenueCells = new List<CellData>();
        for (int i = 0; i < rowCount / 3; i++)
        {
            revenueCells.Add(new CellData { Address = new CellAddress(i, 0), Value = $"Q{(i % 4) + 1} 2024" });
            revenueCells.Add(new CellData { Address = new CellAddress(i, 1), Value = 1000000 + (i * 50000), NumberFormatString = "$#,##0" });
            revenueCells.Add(new CellData { Address = new CellAddress(i, 2), Value = (i % 100) / 100.0, NumberFormatString = "0.00%" });
        }
        
        sheets.Add(new WorksheetContext
        {
            Name = "Revenue",
            Cells = revenueCells,
            Dimensions = (rowCount / 3, 3)
        });
        
        // Dates sheet
        var datesCells = new List<CellData>();
        for (int i = 0; i < rowCount / 3; i++)
        {
            datesCells.Add(new CellData { Address = new CellAddress(i, 0), Value = $"2024-{(i % 12) + 1:D2}-{(i % 28) + 1:D2}" });
            datesCells.Add(new CellData { Address = new CellAddress(i, 1), Value = $"{(i % 12) + 1:D2}:00:00" });
        }
        
        sheets.Add(new WorksheetContext
        {
            Name = "Dates",
            Cells = datesCells,
            Dimensions = (rowCount / 3, 2)
        });
        
        // Scientific data sheet
        var sciCells = new List<CellData>();
        for (int i = 0; i < rowCount / 3; i++)
        {
            sciCells.Add(new CellData { Address = new CellAddress(i, 0), Value = $"{1.23 + i}E+{(i % 10) + 5}" });
            sciCells.Add(new CellData { Address = new CellAddress(i, 1), Value = $"{i + 1}/{(i % 8) + 1}" });
        }
        
        sheets.Add(new WorksheetContext
        {
            Name = "Scientific",
            Cells = sciCells,
            Dimensions = (rowCount / 3, 2)
        });
        
        var totalCells = sheets.Sum(s => s.Cells.Count);
        
        return new WorkbookContext
        {
            Name = "FinancialData",
            Worksheets = sheets,
            Statistics = new WorkbookStatistics
            {
                TotalCells = totalCells,
                EmptyCells = 0,
                TypeDistribution = new Dictionary<Type, int>
                {
                    [typeof(string)] = totalCells / 2,
                    [typeof(decimal)] = totalCells / 2
                }
            }
        };
    }
    
    private static WorkbookContext CreateLargeDataset(int cellCount)
    {
        var cells = new List<CellData>();
        var columns = 100;
        var rows = cellCount / columns;
        
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                var cellIndex = r * columns + c;
                var typeIndex = cellIndex % 10;
                object value = typeIndex switch
                {
                    0 => $"Text{cellIndex}",
                    1 => cellIndex * 1.5m,
                    2 => $"2024-{(cellIndex % 12) + 1:D2}-{(cellIndex % 28) + 1:D2}",
                    3 => (cellIndex % 100) / 100.0,
                    4 => $"${cellIndex * 10}",
                    5 => cellIndex % 2 == 0 ? "true" : "false",
                    6 => $"{cellIndex}.{cellIndex}E+{cellIndex % 10}",
                    7 => $"{cellIndex % 24}:{cellIndex % 60}:00",
                    8 => $"{cellIndex}/{(cellIndex % 8) + 1}",
                    _ => cellIndex.ToString()
                };
                
                var format = typeIndex switch
                {
                    1 => "#,##0.00",
                    3 => "0.00%",
                    4 => "$#,##0",
                    _ => (string?)null
                };
                
                cells.Add(new CellData
                {
                    Address = new CellAddress(r, c),
                    Value = value,
                    NumberFormatString = format
                });
            }
        }
        
        return new WorkbookContext
        {
            Name = "LargeDataset",
            Worksheets = new[]
            {
                new WorksheetContext
                {
                    Name = "Data",
                    Cells = cells,
                    Dimensions = (rows, columns)
                }
            },
            Statistics = new WorkbookStatistics
            {
                TotalCells = cells.Count,
                EmptyCells = 0,
                TypeDistribution = new Dictionary<Type, int>
                {
                    [typeof(string)] = cells.Count / 2,
                    [typeof(decimal)] = cells.Count / 4,
                    [typeof(double)] = cells.Count / 4
                }
            }
        };
    }
    
    private static WorkbookContext CreateVariedDataset(int cellCount)
    {
        var emptyCells = cellCount / 5; // 20% empty
        var filledCells = cellCount - emptyCells;
        
        var cells = new List<CellData>();
        var columns = Math.Min(50, (int)Math.Sqrt(cellCount));
        var rows = cellCount / columns;
        
        var cellIndex = 0;
        for (int r = 0; r < rows && cellIndex < cellCount; r++)
        {
            for (int c = 0; c < columns && cellIndex < cellCount; c++)
            {
                // Skip some cells to create empty spaces
                if (cellIndex < emptyCells && cellIndex % 5 == 0)
                {
                    cellIndex++;
                    continue;
                }
                
                cells.Add(new CellData
                {
                    Address = new CellAddress(r, c),
                    Value = $"Value{cellIndex}",
                    NumberFormatString = cellIndex % 3 == 0 ? "$#,##0.00" : (string?)null
                });
                
                cellIndex++;
            }
        }
        
        return new WorkbookContext
        {
            Name = "VariedDataset",
            Worksheets = new[]
            {
                new WorksheetContext
                {
                    Name = "Data",
                    Cells = cells,
                    Dimensions = (rows, columns)
                }
            },
            Statistics = new WorkbookStatistics
            {
                TotalCells = cellCount,
                EmptyCells = emptyCells,
                TypeDistribution = new Dictionary<Type, int>
                {
                    [typeof(string)] = cells.Count
                }
            }
        };
    }
    
    private static WorkbookContext CreateFormatOptimizedDataset(int rowCount)
    {
        var cells = new List<CellData>();
        
        // Create large blocks of cells with the same format
        // This simulates financial data with consistent formatting
        
        // Headers
        cells.Add(new CellData { Address = new CellAddress(0, 0), Value = "Month" });
        cells.Add(new CellData { Address = new CellAddress(0, 1), Value = "Revenue" });
        cells.Add(new CellData { Address = new CellAddress(0, 2), Value = "Cost" });
        cells.Add(new CellData { Address = new CellAddress(0, 3), Value = "Profit" });
        cells.Add(new CellData { Address = new CellAddress(0, 4), Value = "Margin" });
        
        // Create blocks of data with consistent types
        for (int i = 1; i <= rowCount; i++)
        {
            // Month - only 12 unique values
            cells.Add(new CellData { Address = new CellAddress(i, 0), Value = $"2024-{((i - 1) % 12) + 1:D2}" });
            
            // Financial values with consistent format - values repeat in patterns
            var baseValue = ((i - 1) % 100) * 1000; // Only 100 unique base values
            
            cells.Add(new CellData { Address = new CellAddress(i, 1), Value = baseValue, NumberFormatString = "$#,##0" });
            cells.Add(new CellData { Address = new CellAddress(i, 2), Value = baseValue * 0.7m, NumberFormatString = "$#,##0" });
            cells.Add(new CellData { Address = new CellAddress(i, 3), Value = baseValue * 0.3m, NumberFormatString = "$#,##0" });
            cells.Add(new CellData { Address = new CellAddress(i, 4), Value = 0.30m, NumberFormatString = "0.00%" });
        }
        
        return new WorkbookContext
        {
            Name = "FormatOptimized",
            Worksheets = new[]
            {
                new WorksheetContext
                {
                    Name = "Financial",
                    Cells = cells,
                    Dimensions = (rowCount + 1, 5)
                }
            },
            Statistics = new WorkbookStatistics
            {
                TotalCells = cells.Count,
                EmptyCells = 0,
                TypeDistribution = new Dictionary<Type, int>
                {
                    [typeof(string)] = rowCount + 5,
                    [typeof(decimal)] = rowCount * 4
                }
            }
        };
    }
    
    private static WorkbookContext CreateHighlyRepetitiveDataset(int rowCount)
    {
        var cells = new List<CellData>();
        
        // Create a dataset with very high repetition
        // Headers
        cells.Add(new CellData { Address = new CellAddress(0, 0), Value = "Status" });
        cells.Add(new CellData { Address = new CellAddress(0, 1), Value = "Category" });
        cells.Add(new CellData { Address = new CellAddress(0, 2), Value = "Priority" });
        cells.Add(new CellData { Address = new CellAddress(0, 3), Value = "Assigned" });
        cells.Add(new CellData { Address = new CellAddress(0, 4), Value = "Date" });
        
        // Data with extreme repetition - only a few unique values
        for (int i = 1; i <= rowCount; i++)
        {
            // Only 3 statuses
            cells.Add(new CellData { Address = new CellAddress(i, 0), Value = i % 3 == 0 ? "Active" : i % 3 == 1 ? "Pending" : "Closed" });
            
            // Only 2 categories
            cells.Add(new CellData { Address = new CellAddress(i, 1), Value = i % 2 == 0 ? "TypeA" : "TypeB" });
            
            // Only 3 priorities
            cells.Add(new CellData { Address = new CellAddress(i, 2), Value = i % 3 == 0 ? "High" : i % 3 == 1 ? "Medium" : "Low" });
            
            // Only 5 assigned users
            cells.Add(new CellData { Address = new CellAddress(i, 3), Value = $"User{(i % 5) + 1}" });
            
            // Only 1 date (same date for all)
            cells.Add(new CellData { Address = new CellAddress(i, 4), Value = "2024-01-01" });
        }
        
        return new WorkbookContext
        {
            Name = "RepetitiveData",
            Worksheets = new[]
            {
                new WorksheetContext
                {
                    Name = "Sheet1",
                    Cells = cells,
                    Dimensions = (rowCount + 1, 5)
                }
            },
            Statistics = new WorkbookStatistics
            {
                TotalCells = cells.Count,
                EmptyCells = 0,
                TypeDistribution = new Dictionary<Type, int>
                {
                    [typeof(string)] = cells.Count
                }
            }
        };
    }
    
    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}