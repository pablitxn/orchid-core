// using Application.Interfaces.Spreadsheet;
// using FluentAssertions;
// using Spreadsheet.IntegrationTests.TestUtilities;
// using Xunit;
// using CompressionResult = Spreadsheet.IntegrationTests.TestUtilities.CompressionResult;
//
// namespace Spreadsheet.IntegrationTests;
//
// /// <summary>
// /// Integration tests demonstrating metrics evaluation for spreadsheet processing models.
// /// </summary>
// public class MetricsEvaluationTests
// {
//     private readonly MetricsEvaluator _evaluator;
//
//     public MetricsEvaluationTests()
//     {
//         _evaluator = new MetricsEvaluator();
//     }
//
//     [Fact]
//     public async Task EvaluateTableDetection_WithPerfectPredictions_ShouldReturn100PercentMetrics()
//     {
//         // Arrange
//         var groundTruth = new List<GroundTruthTable>
//         {
//             new() { SheetName = "Sheet1", TopRow = 5, BottomRow = 15, LeftColumn = 2, RightColumn = 8 },
//             new() { SheetName = "Sheet1", TopRow = 20, BottomRow = 30, LeftColumn = 2, RightColumn = 6 }
//         };
//
//         var predictions = new List<DetectedTable>
//         {
//             new() { SheetName = "Sheet1", TopRow = 5, BottomRow = 15, LeftColumn = 2, RightColumn = 8 },
//             new() { SheetName = "Sheet1", TopRow = 20, BottomRow = 30, LeftColumn = 2, RightColumn = 6 }
//         };
//
//         // Act
//         var metrics = _evaluator.EvaluateTableDetection(predictions, groundTruth);
//
//         // Assert
//         metrics.Precision.Should().Be(1.0);
//         metrics.Recall.Should().Be(1.0);
//         metrics.F1Score.Should().Be(1.0);
//         metrics.ExactMatchRate.Should().Be(1.0);
//         metrics.AverageIoU.Should().Be(1.0);
//         metrics.TruePositives.Should().Be(2);
//         metrics.FalsePositives.Should().Be(0);
//         metrics.FalseNegatives.Should().Be(0);
//     }
//
//     [Fact]
//     public async Task EvaluateTableDetection_WithPartialOverlap_ShouldCalculateCorrectIoU()
//     {
//         // Arrange
//         var groundTruth = new List<GroundTruthTable>
//         {
//             new() { SheetName = "Sheet1", TopRow = 5, BottomRow = 15, LeftColumn = 2, RightColumn = 8 }
//         };
//
//         var predictions = new List<DetectedTable>
//         {
//             // Prediction overlaps but is slightly off
//             new() { SheetName = "Sheet1", TopRow = 6, BottomRow = 16, LeftColumn = 2, RightColumn = 7 }
//         };
//
//         // Act
//         var metrics = _evaluator.EvaluateTableDetection(predictions, groundTruth);
//
//         // Assert
//         metrics.Precision.Should().Be(1.0); // All predictions have matches
//         metrics.Recall.Should().Be(1.0); // All ground truth covered
//         metrics.ExactMatchRate.Should().Be(0.0); // Not exact
//         metrics.AverageIoU.Should().BeGreaterThan(0.5).And.BeLessThan(1.0);
//     }
//
//     [Fact]
//     public async Task EvaluateTableDetection_WithMissedTables_ShouldPenalizeRecall()
//     {
//         // Arrange
//         var groundTruth = new List<GroundTruthTable>
//         {
//             new() { SheetName = "Sheet1", TopRow = 5, BottomRow = 15, LeftColumn = 2, RightColumn = 8 },
//             new() { SheetName = "Sheet1", TopRow = 20, BottomRow = 30, LeftColumn = 2, RightColumn = 6 },
//             new() { SheetName = "Sheet2", TopRow = 1, BottomRow = 10, LeftColumn = 1, RightColumn = 5 }
//         };
//
//         var predictions = new List<DetectedTable>
//         {
//             // Only detected first table
//             new() { SheetName = "Sheet1", TopRow = 5, BottomRow = 15, LeftColumn = 2, RightColumn = 8 }
//         };
//
//         // Act
//         var metrics = _evaluator.EvaluateTableDetection(predictions, groundTruth);
//
//         // Assert
//         metrics.Precision.Should().Be(1.0); // What we found was correct
//         metrics.Recall.Should().BeApproximately(0.333, 0.01); // Found 1 of 3
//         metrics.F1Score.Should().BeApproximately(0.5, 0.01);
//         metrics.FalseNegatives.Should().Be(2);
//     }
//
//     [Fact]
//     public async Task EvaluateQuestionAnswering_WithNumericAnswers_ShouldCalculateAccuracy()
//     {
//         // Arrange
//         var groundTruth = new List<QAGroundTruth>
//         {
//             new() { Question = "What is the total revenue?", ExpectedAnswer = "1234567", Tolerance = 0.01 },
//             new() { Question = "What is the Q4 growth?", ExpectedAnswer = "15.5", Tolerance = 0.05 }
//         };
//
//         var predictions = new List<QAPrediction>
//         {
//             new() { Question = "What is the total revenue?", Answer = "$1,234,567", Confidence = 0.95 },
//             new() { Question = "What is the Q4 growth?", Answer = "15.3%", Confidence = 0.90 }
//         };
//
//         // Act
//         var metrics = _evaluator.EvaluateQuestionAnswering(predictions, groundTruth);
//
//         // Assert
//         metrics.ExactMatchRate.Should().Be(0.5); // First answer is exact after normalization
//         metrics.AverageAccuracy.Should().BeGreaterThan(0.9); // Both are very close numerically
//         metrics.TotalQuestions.Should().Be(2);
//         metrics.CorrectAnswers.Should().Be(1);
//     }
//
//     [Fact]
//     public async Task EvaluateQuestionAnswering_WithTextAnswers_ShouldCalculateBLEU()
//     {
//         // Arrange
//         var groundTruth = new List<QAGroundTruth>
//         {
//             new() { Question = "Which department has highest revenue?", ExpectedAnswer = "Engineering department" },
//             new() { Question = "What is the trend?", ExpectedAnswer = "Revenue is increasing steadily across all quarters" }
//         };
//
//         var predictions = new List<QAPrediction>
//         {
//             new() { Question = "Which department has highest revenue?", Answer = "Engineering", Confidence = 0.9 },
//             new() { Question = "What is the trend?", Answer = "Revenue is going up in all quarters", Confidence = 0.85 }
//         };
//
//         // Act
//         var metrics = _evaluator.EvaluateQuestionAnswering(predictions, groundTruth);
//
//         // Assert
//         metrics.ExactMatchRate.Should().Be(0.0); // No exact matches
//         metrics.AverageBLEU.Should().BeGreaterThan(0.3); // Some word overlap
//     }
//
//     [Fact]
//     public async Task EvaluateCompression_ShouldMeasureEffectiveness()
//     {
//         // Arrange
//         var groundTruth = new CompressionGroundTruth
//         {
//             ExpectedCompressionRatio = 10.0,
//             CriticalCells = new List<string> { "A1", "B5", "C10", "D15", "E20" },
//             MaxAcceptableTokens = 1000
//         };
//
//         var result = new CompressionResult
//         {
//             OriginalTokenCount = 10000,
//             CompressedTokenCount = 950,
//             PreservedCells = new List<string> { "A1", "B5", "C10", "E20" }, // Missing D15
//             ProcessingTimeMs = 1500
//         };
//
//         // Act
//         var metrics = _evaluator.EvaluateCompression(result, groundTruth);
//
//         // Assert
//         metrics.ActualCompressionRatio.Should().BeApproximately(10.53, 0.01);
//         metrics.CompressionRatioDeviation.Should().BeApproximately(0.53, 0.01);
//         metrics.TokenReduction.Should().BeApproximately(0.905, 0.001);
//         metrics.CriticalCellsPreserved.Should().Be(0.8); // 4 of 5 preserved
//         metrics.ProcessingTimeMs.Should().Be(1500);
//     }
//
//     [Fact]
//     public async Task GenerateReport_ShouldProduceComprehensiveReport()
//     {
//         // Arrange
//         // Run some evaluations
//         var tableGroundTruth = new List<GroundTruthTable>
//         {
//             new() { SheetName = "Sheet1", TopRow = 5, BottomRow = 15, LeftColumn = 2, RightColumn = 8 }
//         };
//         var tablePredictions = new List<DetectedTable>
//         {
//             new() { SheetName = "Sheet1", TopRow = 5, BottomRow = 15, LeftColumn = 2, RightColumn = 8 }
//         };
//
//         var qaGroundTruth = new List<QAGroundTruth>
//         {
//             new() { Question = "Test?", ExpectedAnswer = "42" }
//         };
//         var qaPredictions = new List<QAPrediction>
//         {
//             new() { Question = "Test?", Answer = "42" }
//         };
//
//         // Act
//         var tableMetrics = _evaluator.EvaluateTableDetection(tablePredictions, tableGroundTruth);
//         var qaMetrics = _evaluator.EvaluateQuestionAnswering(qaPredictions, qaGroundTruth);
//         
//         var report = _evaluator.GenerateReport("Integration Test Suite");
//
//         // Assert
//         report.Should().NotBeNull();
//         report.TestName.Should().Be("Integration Test Suite");
//         report.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
//         report.Summary.Should().NotBeNull();
//     }
//
//     [Fact]
//     public async Task SaveAndLoadGroundTruth_ShouldPersistCorrectly()
//     {
//         // Arrange
//         var tempFile = Path.GetTempFileName();
//         var groundTruth = new List<GroundTruthTable>
//         {
//             new() 
//             { 
//                 SheetName = "Financial Data", 
//                 TopRow = 5, 
//                 BottomRow = 25, 
//                 LeftColumn = 2, 
//                 RightColumn = 10,
//                 Description = "Quarterly revenue by department"
//             }
//         };
//
//         try
//         {
//             // Act - Save
//             await File.WriteAllTextAsync(tempFile, System.Text.Json.JsonSerializer.Serialize(groundTruth));
//             
//             // Act - Load
//             var loaded = await _evaluator.LoadGroundTruth<List<GroundTruthTable>>(tempFile);
//
//             // Assert
//             loaded.Should().HaveCount(1);
//             loaded[0].SheetName.Should().Be("Financial Data");
//             loaded[0].Description.Should().Be("Quarterly revenue by department");
//             loaded[0].TopRow.Should().Be(5);
//         }
//         finally
//         {
//             File.Delete(tempFile);
//         }
//     }
//
//     [Theory]
//     [InlineData(100, 10, 10.0)]
//     [InlineData(1000, 50, 20.0)]
//     [InlineData(5000, 100, 50.0)]
//     public void EvaluateCompression_WithVariousRatios_ShouldCalculateCorrectly(
//         int originalTokens, int compressedTokens, double expectedRatio)
//     {
//         // Arrange
//         var result = new CompressionResult
//         {
//             OriginalTokenCount = originalTokens,
//             CompressedTokenCount = compressedTokens
//         };
//
//         var groundTruth = new CompressionGroundTruth
//         {
//             ExpectedCompressionRatio = expectedRatio
//         };
//
//         // Act
//         var metrics = _evaluator.EvaluateCompression(result, groundTruth);
//
//         // Assert
//         var actualRatio = (double)originalTokens / compressedTokens;
//         metrics.ActualCompressionRatio.Should().BeApproximately(actualRatio, 0.001);
//         metrics.TokenReduction.Should().BeApproximately(1.0 - (double)compressedTokens / originalTokens, 0.001);
//     }
// }