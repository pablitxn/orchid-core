using System.Text.Json;
using Application.Interfaces.Spreadsheet;
using Domain.ValueObjects.Spreadsheet;

namespace Spreadsheet.IntegrationTests.TestUtilities;

/// <summary>
/// Evaluates model predictions against ground truth for spreadsheet processing tasks.
/// Calculates precision, recall, F1-score, and task-specific metrics.
/// </summary>
public class MetricsEvaluator
{
    private readonly List<EvaluationResult> _results = new();

    /// <summary>
    /// Evaluate table detection results.
    /// </summary>
    public TableDetectionMetrics EvaluateTableDetection(
        List<DetectedTable> predictions, 
        List<GroundTruthTable> groundTruth)
    {
        var metrics = new TableDetectionMetrics();
        
        // Calculate true positives, false positives, false negatives
        var truePositives = 0;
        var falsePositives = 0;
        var falseNegatives = 0;
        var exactMatches = 0;

        var matchedGroundTruth = new HashSet<int>();

        foreach (var pred in predictions)
        {
            var matchIndex = FindBestMatch(pred, groundTruth);
            
            if (matchIndex >= 0)
            {
                truePositives++;
                matchedGroundTruth.Add(matchIndex);
                
                // Check if it's an exact match
                if (IsExactMatch(pred, groundTruth[matchIndex]))
                {
                    exactMatches++;
                }
            }
            else
            {
                falsePositives++;
            }
        }

        falseNegatives = groundTruth.Count - matchedGroundTruth.Count;

        // Calculate metrics
        metrics.TruePositives = truePositives;
        metrics.FalsePositives = falsePositives;
        metrics.FalseNegatives = falseNegatives;
        metrics.ExactMatches = exactMatches;

        metrics.Precision = truePositives + falsePositives > 0 
            ? (double)truePositives / (truePositives + falsePositives) 
            : 0;
            
        metrics.Recall = truePositives + falseNegatives > 0 
            ? (double)truePositives / (truePositives + falseNegatives) 
            : 0;
            
        metrics.F1Score = metrics.Precision + metrics.Recall > 0 
            ? 2 * (metrics.Precision * metrics.Recall) / (metrics.Precision + metrics.Recall) 
            : 0;
            
        metrics.ExactMatchRate = groundTruth.Count > 0 
            ? (double)exactMatches / groundTruth.Count 
            : 0;

        // Calculate IoU (Intersection over Union) for matched tables
        var ious = new List<double>();
        foreach (var pred in predictions)
        {
            var matchIndex = FindBestMatch(pred, groundTruth);
            if (matchIndex >= 0)
            {
                var iou = CalculateIoU(pred, groundTruth[matchIndex]);
                ious.Add(iou);
            }
        }
        
        metrics.AverageIoU = ious.Any() ? ious.Average() : 0;

        return metrics;
    }

    /// <summary>
    /// Evaluate question-answering results.
    /// </summary>
    public QuestionAnsweringMetrics EvaluateQuestionAnswering(
        List<QAPrediction> predictions,
        List<QAGroundTruth> groundTruth)
    {
        var metrics = new QuestionAnsweringMetrics();
        
        if (!predictions.Any() || !groundTruth.Any())
        {
            return metrics;
        }

        var exactMatches = 0;
        var totalQuestions = Math.Min(predictions.Count, groundTruth.Count);
        var accuracyScores = new List<double>();
        var bleuScores = new List<double>();

        for (int i = 0; i < totalQuestions; i++)
        {
            var pred = predictions[i];
            var truth = groundTruth[i];

            // Exact match
            if (NormalizeAnswer(pred.Answer) == NormalizeAnswer(truth.ExpectedAnswer))
            {
                exactMatches++;
            }

            // Accuracy score (for numerical answers)
            if (TryParseNumericAnswer(pred.Answer, out var predValue) && 
                TryParseNumericAnswer(truth.ExpectedAnswer, out var truthValue))
            {
                var accuracy = CalculateNumericAccuracy(predValue, truthValue, truth.Tolerance ?? 0.01);
                accuracyScores.Add(accuracy);
            }

            // BLEU score for text similarity
            var bleu = CalculateSimplifiedBLEU(pred.Answer, truth.ExpectedAnswer);
            bleuScores.Add(bleu);
        }

        metrics.ExactMatchRate = (double)exactMatches / totalQuestions;
        metrics.AverageAccuracy = accuracyScores.Any() ? accuracyScores.Average() : 0;
        metrics.AverageBLEU = bleuScores.Any() ? bleuScores.Average() : 0;
        metrics.TotalQuestions = totalQuestions;
        metrics.CorrectAnswers = exactMatches;

        return metrics;
    }

    /// <summary>
    /// Evaluate compression effectiveness.
    /// </summary>
    public CompressionMetrics EvaluateCompression(
        CompressionResult prediction,
        CompressionGroundTruth groundTruth)
    {
        var metrics = new CompressionMetrics();

        metrics.ActualCompressionRatio = (double)prediction.OriginalTokenCount / prediction.CompressedTokenCount;
        metrics.ExpectedCompressionRatio = groundTruth.ExpectedCompressionRatio;
        metrics.CompressionRatioDeviation = Math.Abs(metrics.ActualCompressionRatio - metrics.ExpectedCompressionRatio);
        
        metrics.TokenReduction = 1.0 - ((double)prediction.CompressedTokenCount / prediction.OriginalTokenCount);
        metrics.ProcessingTimeMs = prediction.ProcessingTimeMs;
        
        // Information preservation metrics
        if (groundTruth.CriticalCells != null && prediction.PreservedCells != null)
        {
            var preservedCritical = groundTruth.CriticalCells.Intersect(prediction.PreservedCells).Count();
            metrics.CriticalCellsPreserved = (double)preservedCritical / groundTruth.CriticalCells.Count;
        }

        return metrics;
    }

    /// <summary>
    /// Generate a comprehensive evaluation report.
    /// </summary>
    public EvaluationReport GenerateReport(string testName)
    {
        return new EvaluationReport
        {
            TestName = testName,
            Timestamp = DateTime.UtcNow,
            Results = _results.ToList(),
            Summary = GenerateSummary()
        };
    }

    /// <summary>
    /// Save evaluation report to JSON file.
    /// </summary>
    public async Task SaveReport(EvaluationReport report, string filePath)
    {
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Load ground truth from JSON file.
    /// </summary>
    public async Task<T> LoadGroundTruth<T>(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException($"Failed to deserialize {filePath}");
    }

    // Helper methods
    private int FindBestMatch(DetectedTable prediction, List<GroundTruthTable> groundTruth)
    {
        var bestMatch = -1;
        var bestIoU = 0.0;

        for (int i = 0; i < groundTruth.Count; i++)
        {
            if (prediction.SheetName != groundTruth[i].SheetName)
                continue;

            var iou = CalculateIoU(prediction, groundTruth[i]);
            if (iou > bestIoU && iou > 0.5) // Threshold for considering a match
            {
                bestIoU = iou;
                bestMatch = i;
            }
        }

        return bestMatch;
    }

    private bool IsExactMatch(DetectedTable prediction, GroundTruthTable groundTruth)
    {
        return prediction.TopRow == groundTruth.TopRow &&
               prediction.BottomRow == groundTruth.BottomRow &&
               prediction.LeftColumn == groundTruth.LeftColumn &&
               prediction.RightColumn == groundTruth.RightColumn;
    }

    private double CalculateIoU(DetectedTable pred, GroundTruthTable truth)
    {
        // Calculate intersection
        var intersectTop = Math.Max(pred.TopRow, truth.TopRow);
        var intersectBottom = Math.Min(pred.BottomRow, truth.BottomRow);
        var intersectLeft = Math.Max(pred.LeftColumn, truth.LeftColumn);
        var intersectRight = Math.Min(pred.RightColumn, truth.RightColumn);

        if (intersectTop > intersectBottom || intersectLeft > intersectRight)
            return 0; // No intersection

        var intersectionArea = (intersectBottom - intersectTop + 1) * (intersectRight - intersectLeft + 1);

        // Calculate union
        var predArea = (pred.BottomRow - pred.TopRow + 1) * (pred.RightColumn - pred.LeftColumn + 1);
        var truthArea = (truth.BottomRow - truth.TopRow + 1) * (truth.RightColumn - truth.LeftColumn + 1);
        var unionArea = predArea + truthArea - intersectionArea;

        return (double)intersectionArea / unionArea;
    }

    private string NormalizeAnswer(string answer)
    {
        return answer.Trim().ToLowerInvariant()
            .Replace(",", "")
            .Replace("$", "")
            .Replace("%", "");
    }

    private bool TryParseNumericAnswer(string answer, out double value)
    {
        var normalized = NormalizeAnswer(answer);
        return double.TryParse(normalized, out value);
    }

    private double CalculateNumericAccuracy(double prediction, double truth, double tolerance)
    {
        var error = Math.Abs(prediction - truth);
        var relativeError = truth != 0 ? error / Math.Abs(truth) : error;
        
        if (relativeError <= tolerance)
            return 1.0;
        
        // Linear decay up to 2x tolerance
        if (relativeError <= tolerance * 2)
            return 1.0 - (relativeError - tolerance) / tolerance;
        
        return 0.0;
    }

    private double CalculateSimplifiedBLEU(string prediction, string reference)
    {
        var predTokens = prediction.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var refTokens = reference.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (!predTokens.Any() || !refTokens.Any())
            return 0;

        // Unigram precision
        var matches = predTokens.Count(t => refTokens.Contains(t));
        var precision = (double)matches / predTokens.Length;

        // Brevity penalty
        var brevityPenalty = Math.Min(1.0, Math.Exp(1 - (double)refTokens.Length / predTokens.Length));

        return precision * brevityPenalty;
    }

    private EvaluationSummary GenerateSummary()
    {
        return new EvaluationSummary
        {
            TotalTests = _results.Count,
            PassedTests = _results.Count(r => r.Passed),
            AverageScore = _results.Any() ? _results.Average(r => r.Score) : 0,
            Timestamp = DateTime.UtcNow
        };
    }
}

// Metric classes
public class TableDetectionMetrics
{
    public int TruePositives { get; set; }
    public int FalsePositives { get; set; }
    public int FalseNegatives { get; set; }
    public int ExactMatches { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public double ExactMatchRate { get; set; }
    public double AverageIoU { get; set; }
}

public class QuestionAnsweringMetrics
{
    public double ExactMatchRate { get; set; }
    public double AverageAccuracy { get; set; }
    public double AverageBLEU { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
}

public class CompressionMetrics
{
    public double ActualCompressionRatio { get; set; }
    public double ExpectedCompressionRatio { get; set; }
    public double CompressionRatioDeviation { get; set; }
    public double TokenReduction { get; set; }
    public double CriticalCellsPreserved { get; set; }
    public long ProcessingTimeMs { get; set; }
}

// Ground truth classes
public class GroundTruthTable
{
    public string SheetName { get; set; } = "";
    public int TopRow { get; set; }
    public int BottomRow { get; set; }
    public int LeftColumn { get; set; }
    public int RightColumn { get; set; }
    public string? Description { get; set; }
}

public class QAGroundTruth
{
    public string Question { get; set; } = "";
    public string ExpectedAnswer { get; set; } = "";
    public double? Tolerance { get; set; }
    public string? AnswerType { get; set; }
}

public class CompressionGroundTruth
{
    public double ExpectedCompressionRatio { get; set; }
    public List<string>? CriticalCells { get; set; }
    public int MaxAcceptableTokens { get; set; }
}

// Prediction classes
public class QAPrediction
{
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public double Confidence { get; set; }
}

public class CompressionResult
{
    public int OriginalTokenCount { get; set; }
    public int CompressedTokenCount { get; set; }
    public List<string>? PreservedCells { get; set; }
    public long ProcessingTimeMs { get; set; }
}

// Report classes
public class EvaluationResult
{
    public string TestName { get; set; } = "";
    public bool Passed { get; set; }
    public double Score { get; set; }
    public object? Metrics { get; set; }
    public string? Notes { get; set; }
}

public class EvaluationReport
{
    public string TestName { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public List<EvaluationResult> Results { get; set; } = new();
    public EvaluationSummary Summary { get; set; } = new();
}

public class EvaluationSummary
{
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public double AverageScore { get; set; }
    public DateTime Timestamp { get; set; }
}