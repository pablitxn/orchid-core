// using Application.Interfaces;
// using Application.UseCases.Spreadsheets.CompressWorkbook;
// using FluentAssertions;
// using Microsoft.Extensions.DependencyInjection;
// using Moq;
// using Spreadsheet.IntegrationTests.TestUtilities;
// using Xunit;
//
// namespace Spreadsheet.IntegrationTests;
//
// /// <summary>
// /// Integration tests using synthetic spreadsheet data to validate compression and processing.
// /// </summary>
// public class SyntheticSpreadsheetTests
// {
//     private readonly Mock<IVanillaSerializer> _vanillaSerializer = new();
//     private readonly Mock<IStructuralAnchorDetector> _anchorDetector = new();
//     private readonly Mock<ISkeletonExtractor> _skeletonExtractor = new();
//
//     [Fact]
//     public async Task SyntheticBuilder_ShouldGenerateFinancialReportStructure()
//     {
//         // Arrange
//         var builder = new SyntheticSpreadsheetBuilder(seed: 123);
//         
//         // Act
//         var workbook = builder
//             .AddSheet("Q4 Financial Report", sheet => sheet
//                 .WithDimensions(100, 20)
//                 .WithEmptyCellProbability(0.05)
//                 .AddTable(table => table
//                     .At(row: 5, column: 2)
//                     .WithRows(12)
//                     .WithHeaderStyle("Bold")
//                     .AddColumn("Department", SyntheticDataType.Text, col => col
//                         .WithPossibleValues("Sales", "Marketing", "Engineering", "Operations", "HR", "Finance"))
//                     .AddColumn("Q1 Revenue", SyntheticDataType.Currency, col => col
//                         .WithRange(50000, 500000)
//                         .WithFormat("$#,##0.00"))
//                     .AddColumn("Q2 Revenue", SyntheticDataType.Currency, col => col
//                         .WithRange(50000, 500000)
//                         .WithFormat("$#,##0.00"))
//                     .AddColumn("Q3 Revenue", SyntheticDataType.Currency, col => col
//                         .WithRange(50000, 500000)
//                         .WithFormat("$#,##0.00"))
//                     .AddColumn("Q4 Revenue", SyntheticDataType.Currency, col => col
//                         .WithRange(50000, 500000)
//                         .WithFormat("$#,##0.00"))
//                     .AddColumn("YoY Growth", SyntheticDataType.Percentage, col => col
//                         .WithRange(-20, 50)
//                         .WithFormat("0.00%")))
//                 .AddTable(table => table
//                     .At(row: 25, column: 2)
//                     .WithRows(5)
//                     .AddColumn("Metric", SyntheticDataType.Text, col => col
//                         .WithPossibleValues("Total Revenue", "Total Expenses", "Net Profit", "EBITDA", "Cash Flow"))
//                     .AddColumn("Value", SyntheticDataType.Currency, col => col
//                         .WithRange(1000000, 10000000)
//                         .WithFormat("$#,##0"))
//                     .AddColumn("Target", SyntheticDataType.Currency, col => col
//                         .WithRange(1000000, 10000000)
//                         .WithFormat("$#,##0"))
//                     .AddColumn("Variance", SyntheticDataType.Percentage, col => col
//                         .WithRange(-10, 10)
//                         .WithFormat("0.0%"))))
//             .Build();
//
//         // Assert
//         workbook.Should().NotBeNull();
//         workbook.Worksheets.Should().HaveCount(1);
//         
//         var sheet = workbook.Worksheets.First();
//         sheet.Name.Should().Be("Q4 Financial Report");
//         sheet.Cells.Should().NotBeEmpty();
//         
//         // Verify financial data structure
//         var currencyCells = sheet.Cells.Where(c => c.NumberFormatString?.Contains("$") == true).ToList();
//         currencyCells.Should().NotBeEmpty();
//         
//         var percentageCells = sheet.Cells.Where(c => c.NumberFormatString?.Contains("%") == true).ToList();
//         percentageCells.Should().NotBeEmpty();
//         
//         // Verify table headers exist
//         var headers = sheet.Cells.Where(c => c.Value.DataType == CellDataType.String && 
//                                             (c.Value.StringValue == "Department" || c.Value.StringValue == "Metric")).ToList();
//         headers.Should().HaveCount(2);
//     }
//
//     [Fact]
//     public async Task SyntheticBuilder_ShouldGenerateMultiSheetWorkbook()
//     {
//         // Arrange
//         var builder = new SyntheticSpreadsheetBuilder(seed: 456);
//         
//         // Act
//         var workbook = builder
//             .AddSheet("Sales Data", sheet => sheet
//                 .WithDimensions(50, 10)
//                 .AddTable(table => table
//                     .At(row: 2, column: 1)
//                     .WithRows(20)
//                     .AddColumn("Date", SyntheticDataType.Date)
//                     .AddColumn("Product", SyntheticDataType.Text)
//                     .AddColumn("Quantity", SyntheticDataType.Integer, col => col.WithRange(1, 100))
//                     .AddColumn("Unit Price", SyntheticDataType.Currency)
//                     .AddColumn("Total", SyntheticDataType.Currency)))
//             .AddSheet("Inventory", sheet => sheet
//                 .WithDimensions(30, 8)
//                 .WithSparseData(0.1)
//                 .AddTable(table => table
//                     .At(row: 3, column: 2)
//                     .WithRows(15)
//                     .AddColumn("SKU", SyntheticDataType.Text)
//                     .AddColumn("Description", SyntheticDataType.Text)
//                     .AddColumn("Stock", SyntheticDataType.Integer, col => col.WithRange(0, 1000))
//                     .AddColumn("Reorder Level", SyntheticDataType.Integer, col => col.WithRange(10, 100))
//                     .AddColumn("In Transit", SyntheticDataType.Boolean)))
//             .AddSheet("Summary", sheet => sheet
//                 .WithDimensions(20, 5)
//                 .WithEmptyCellProbability(0.3))
//             .Build();
//
//         // Assert
//         workbook.Worksheets.Should().HaveCount(3);
//         workbook.Worksheets.Select(w => w.Name).Should().BeEquivalentTo(new[] { "Sales Data", "Inventory", "Summary" });
//         
//         // Verify each sheet has different characteristics
//         var salesSheet = workbook.Worksheets.First(w => w.Name == "Sales Data");
//         var inventorySheet = workbook.Worksheets.First(w => w.Name == "Inventory");
//         var summarySheet = workbook.Worksheets.First(w => w.Name == "Summary");
//         
//         salesSheet.Cells.Should().NotBeEmpty();
//         inventorySheet.Cells.Should().NotBeEmpty();
//         
//         // Inventory should have boolean values
//         var booleanCells = inventorySheet.Cells.Where(c => c.Value.DataType == CellDataType.Boolean);
//         booleanCells.Should().NotBeEmpty();
//         
//         // Summary should have higher empty cell ratio due to configuration
//         var summaryEmptyRatio = (double)summarySheet.Cells.Count(c => c.Value.DataType == CellDataType.Empty) / summarySheet.Cells.Count;
//         summaryEmptyRatio.Should().BeGreaterThan(0.2);
//     }
//
//     [Fact]
//     public async Task SyntheticBuilder_ShouldGenerateEdgeCaseStructures()
//     {
//         // Arrange
//         var builder = new SyntheticSpreadsheetBuilder(seed: 789);
//         
//         // Act - Create edge case scenarios
//         var workbook = builder
//             .AddSheet("Edge Cases", sheet => sheet
//                 .WithDimensions(200, 50)
//                 .WithEmptyCellProbability(0.8) // Very sparse
//                 .WithSparseData(0.05)
//                 .AddTable(table => table
//                     .At(row: 1, column: 1) // Table at top-left
//                     .WithRows(3)
//                     .AddColumn("A", SyntheticDataType.Integer))
//                 .AddTable(table => table
//                     .At(row: 190, column: 45) // Table at bottom-right
//                     .WithRows(5)
//                     .AddColumn("Z", SyntheticDataType.Text))
//                 .AddTable(table => table
//                     .At(row: 50, column: 25) // Table in middle
//                     .WithRows(100) // Large table
//                     .AddColumn("ID", SyntheticDataType.Integer)
//                     .AddColumn("Value1", SyntheticDataType.Decimal)
//                     .AddColumn("Value2", SyntheticDataType.Decimal)
//                     .AddColumn("Value3", SyntheticDataType.Decimal)
//                     .AddColumn("Status", SyntheticDataType.Text)))
//             .Build();
//
//         // Assert
//         var sheet = workbook.Worksheets.First();
//         
//         // Verify extreme positions
//         var topLeftCells = sheet.Cells.Where(c => c.Address.Row == 1 && c.Address.Column == 1);
//         topLeftCells.Should().NotBeEmpty();
//         
//         var bottomRightCells = sheet.Cells.Where(c => c.Address.Row >= 190 && c.Address.Column >= 45);
//         bottomRightCells.Should().NotBeEmpty();
//         
//         // Verify sparsity
//         var emptyRatio = (double)sheet.Cells.Count(c => c.Value.DataType == CellDataType.Empty) / sheet.Cells.Count;
//         emptyRatio.Should().BeGreaterThan(0.5); // Should be quite sparse
//     }
//
//     [Theory]
//     [InlineData(100, 10, 0.1)] // Small sheet, low sparsity
//     [InlineData(1000, 26, 0.3)] // Medium sheet, medium sparsity
//     [InlineData(5000, 50, 0.7)] // Large sheet, high sparsity
//     public async Task SyntheticBuilder_ShouldHandleVariousSheetSizes(int rows, int columns, double emptyCellProbability)
//     {
//         // Arrange
//         var builder = new SyntheticSpreadsheetBuilder();
//         
//         // Act
//         var workbook = builder
//             .AddSheet($"Test_{rows}x{columns}", sheet => sheet
//                 .WithDimensions(rows, columns)
//                 .WithEmptyCellProbability(emptyCellProbability)
//                 .AddTable(table => table
//                     .At(row: 1, column: 1)
//                     .WithRows(Math.Min(rows - 1, 100)) // Limit table size
//                     .AddColumn("Col1", SyntheticDataType.Integer)
//                     .AddColumn("Col2", SyntheticDataType.Text)
//                     .AddColumn("Col3", SyntheticDataType.Currency)))
//             .Build();
//
//         // Assert
//         var sheet = workbook.Worksheets.First();
//         sheet.Dimensions.MaxRow.Should().BeLessOrEqualTo(rows);
//         sheet.Dimensions.MaxColumn.Should().BeLessOrEqualTo(columns);
//         
//         // Verify empty cell probability is roughly respected
//         if (sheet.Cells.Count > 100) // Only check for larger datasets
//         {
//             var actualEmptyRatio = (double)sheet.Cells.Count(c => c.Value.DataType == CellDataType.Empty) / sheet.Cells.Count;
//             actualEmptyRatio.Should().BeApproximately(emptyCellProbability, 0.2); // Allow 20% variance
//         }
//     }
//
//     [Fact]
//     public async Task SyntheticBuilder_WithSaveAsCsv_ShouldProduceValidCsvFiles()
//     {
//         // Arrange
//         var tempDir = Path.Combine(Path.GetTempPath(), $"synthetic_test_{Guid.NewGuid()}");
//         Directory.CreateDirectory(tempDir);
//         
//         try
//         {
//             var builder = new SyntheticSpreadsheetBuilder();
//             
//             // Act
//             var csvPath = await builder
//                 .AddSheet("Export Test", sheet => sheet
//                     .WithDimensions(20, 5)
//                     .AddTable(table => table
//                         .At(row: 2, column: 1)
//                         .WithRows(10)
//                         .AddColumn("Name", SyntheticDataType.Text)
//                         .AddColumn("Score", SyntheticDataType.Integer, col => col.WithRange(0, 100))
//                         .AddColumn("Grade", SyntheticDataType.Text, col => col
//                             .WithPossibleValues("A", "B", "C", "D", "F"))))
//                 .BuildAndSaveAsCsv(tempDir);
//
//             // Assert
//             File.Exists(csvPath).Should().BeTrue();
//             
//             var csvContent = await File.ReadAllTextAsync(csvPath);
//             csvContent.Should().NotBeEmpty();
//             csvContent.Should().Contain("Name");
//             csvContent.Should().Contain("Score");
//             csvContent.Should().Contain("Grade");
//             
//             // Verify CSV structure
//             var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
//             lines.Should().HaveCountGreaterThan(1); // At least header + data
//         }
//         finally
//         {
//             // Cleanup
//             if (Directory.Exists(tempDir))
//             {
//                 Directory.Delete(tempDir, recursive: true);
//             }
//         }
//     }
// }