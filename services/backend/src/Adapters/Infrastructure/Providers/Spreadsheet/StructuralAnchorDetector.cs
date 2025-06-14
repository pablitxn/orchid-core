using System.Collections.Concurrent;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

/// <summary>
/// Detects structural anchors in worksheets for intelligent compression.
/// </summary>
public sealed class StructuralAnchorDetector : IStructuralAnchorDetector
{
    private readonly ILogger<StructuralAnchorDetector> _logger;

    public StructuralAnchorDetector(ILogger<StructuralAnchorDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StructuralAnchors> FindAnchorsAsync(
        WorksheetContext worksheet,
        int k = 2,
        AnchorDetectionOptions? options = null)
    {
        options ??= new AnchorDetectionOptions();
        
        _logger.LogDebug("Finding anchors in worksheet '{WorksheetName}' with k={K}", worksheet.Name, k);

        var anchors = new StructuralAnchors
        {
            AnchorRows = new HashSet<int>(),
            AnchorColumns = new HashSet<int>(),
            HeaderRegions = new List<HeaderRegion>(),
            Metrics = new AnchorMetrics()
        };

        // Early exit for empty worksheets
        if (worksheet.Cells.Count == 0)
        {
            _logger.LogDebug("Worksheet is empty, no anchors detected");
            return anchors;
        }

        // Analyze rows and columns in parallel
        var rowTask = Task.Run(() => AnalyzeRows(worksheet, options));
        var colTask = Task.Run(() => AnalyzeColumns(worksheet, options));
        var headerTask = Task.Run(() => DetectHeaders(worksheet, options));

        await Task.WhenAll(rowTask, colTask, headerTask);

        var rowAnalysis = rowTask.Result;
        var colAnalysis = colTask.Result;
        var headerRegions = headerTask.Result;

        // Apply k-neighborhood expansion
        var expandedRows = ExpandAnchors(rowAnalysis.Anchors, k, worksheet.Dimensions.LastRowIndex);
        var expandedColumns = ExpandAnchors(colAnalysis.Anchors, k, worksheet.Dimensions.LastColumnIndex);

        // Calculate metrics
        var metrics = CalculateMetrics(expandedRows, expandedColumns, headerRegions, worksheet, rowAnalysis, colAnalysis);

        // Create final anchors object
        anchors = new StructuralAnchors
        {
            AnchorRows = expandedRows,
            AnchorColumns = expandedColumns,
            HeaderRegions = headerRegions,
            Metrics = metrics
        };

        _logger.LogInformation(
            "Found {RowAnchors} row anchors and {ColAnchors} column anchors in worksheet '{WorksheetName}'",
            anchors.AnchorRows.Count,
            anchors.AnchorColumns.Count,
            worksheet.Name);

        return anchors;
    }

    public async Task<WorkbookAnchors> FindWorkbookAnchorsAsync(
        WorkbookContext context,
        int k = 2,
        AnchorDetectionOptions? options = null)
    {
        options ??= new AnchorDetectionOptions();
        
        _logger.LogDebug("Finding anchors across workbook with {SheetCount} sheets", context.Worksheets.Count);

        var worksheetAnchors = new ConcurrentDictionary<string, StructuralAnchors>();
        
        // Process worksheets in parallel
        var tasks = context.Worksheets.Select(async worksheet =>
        {
            var anchors = await FindAnchorsAsync(worksheet, k, options);
            worksheetAnchors.TryAdd(worksheet.Name, anchors);
        });

        await Task.WhenAll(tasks);

        // Detect cross-sheet patterns
        var crossSheetPatterns = DetectCrossSheetPatterns(worksheetAnchors, context);

        // Calculate global metrics
        var globalMetrics = CalculateGlobalMetrics(worksheetAnchors, context);

        return new WorkbookAnchors
        {
            WorksheetAnchors = new Dictionary<string, StructuralAnchors>(worksheetAnchors),
            CrossSheetPatterns = crossSheetPatterns,
            GlobalMetrics = globalMetrics
        };
    }

    private RowColumnAnalysis AnalyzeRows(WorksheetContext worksheet, AnchorDetectionOptions options)
    {
        var analysis = new RowColumnAnalysis();
        var dims = worksheet.Dimensions;
        
        // Group cells by row
        var cellsByRow = worksheet.Cells.Values
            .GroupBy(c => c.RowIndex)
            .OrderBy(g => g.Key)
            .ToList();

        for (var i = 0; i < cellsByRow.Count; i++)
        {
            var row = cellsByRow[i];
            var rowIndex = row.Key;
            var cells = row.ToList();
            
            // Calculate heterogeneity score
            var score = CalculateRowHeterogeneity(cells, options);
            analysis.HeterogeneityScores[rowIndex] = score;

            if (score >= options.MinHeterogeneityScore)
            {
                analysis.Anchors.Add(rowIndex);
            }

            // Check for sudden changes with neighbors
            if (i > 0)
            {
                var prevRow = cellsByRow[i - 1];
                var prevCells = prevRow.ToList();
                
                if (HasSignificantChange(prevCells, cells, options))
                {
                    analysis.Anchors.Add(rowIndex);
                    analysis.Anchors.Add(prevRow.Key);
                }
            }
        }

        return analysis;
    }

    private RowColumnAnalysis AnalyzeColumns(WorksheetContext worksheet, AnchorDetectionOptions options)
    {
        var analysis = new RowColumnAnalysis();
        var dims = worksheet.Dimensions;
        
        // Group cells by column
        var cellsByColumn = worksheet.Cells.Values
            .GroupBy(c => c.ColumnIndex)
            .OrderBy(g => g.Key)
            .ToList();

        for (var i = 0; i < cellsByColumn.Count; i++)
        {
            var col = cellsByColumn[i];
            var colIndex = col.Key;
            var cells = col.ToList();
            
            // Calculate heterogeneity score
            var score = CalculateColumnHeterogeneity(cells, options);
            analysis.HeterogeneityScores[colIndex] = score;

            if (score >= options.MinHeterogeneityScore)
            {
                analysis.Anchors.Add(colIndex);
            }

            // Check for sudden changes with neighbors
            if (i > 0)
            {
                var prevCol = cellsByColumn[i - 1];
                var prevCells = prevCol.ToList();
                
                if (HasSignificantChange(prevCells, cells, options))
                {
                    analysis.Anchors.Add(colIndex);
                    analysis.Anchors.Add(prevCol.Key);
                }
            }
        }

        return analysis;
    }

    private List<HeaderRegion> DetectHeaders(WorksheetContext worksheet, AnchorDetectionOptions options)
    {
        var headers = new List<HeaderRegion>();
        
        if (!options.DetectMultiLevelHeaders)
        {
            // Simple header detection - look for first row with mostly text
            var firstNonEmptyRow = worksheet.Cells.Values
                .Where(c => c.DataType != CellDataType.Empty)
                .MinBy(c => c.RowIndex);

            if (firstNonEmptyRow != null)
            {
                var rowCells = worksheet.Cells.Values
                    .Where(c => c.RowIndex == firstNonEmptyRow.RowIndex)
                    .ToList();

                if (IsLikelyHeader(rowCells))
                {
                    headers.Add(new HeaderRegion
                    {
                        StartRow = firstNonEmptyRow.RowIndex,
                        EndRow = firstNonEmptyRow.RowIndex,
                        StartColumn = rowCells.Min(c => c.ColumnIndex),
                        EndColumn = rowCells.Max(c => c.ColumnIndex),
                        Confidence = 0.8,
                        Type = HeaderType.Simple
                    });
                }
            }
        }
        else
        {
            // Multi-level header detection
            headers.AddRange(DetectMultiLevelHeaders(worksheet, options));
        }

        return headers;
    }

    private List<HeaderRegion> DetectMultiLevelHeaders(WorksheetContext worksheet, AnchorDetectionOptions options)
    {
        var headers = new List<HeaderRegion>();
        var dims = worksheet.Dimensions;
        
        // Look for consecutive rows at the top that look like headers
        var topRows = worksheet.Cells.Values
            .Where(c => c.RowIndex <= dims.FirstRowIndex + options.MaxHeaderDepth)
            .GroupBy(c => c.RowIndex)
            .OrderBy(g => g.Key)
            .ToList();

        var headerRowIndices = new List<int>();
        
        foreach (var row in topRows)
        {
            var cells = row.ToList();
            if (IsLikelyHeader(cells))
            {
                headerRowIndices.Add(row.Key);
            }
            else if (headerRowIndices.Count > 0)
            {
                // Stop when we hit a non-header row
                break;
            }
        }

        if (headerRowIndices.Count > 0)
        {
            var allHeaderCells = worksheet.Cells.Values
                .Where(c => headerRowIndices.Contains(c.RowIndex))
                .ToList();

            headers.Add(new HeaderRegion
            {
                StartRow = headerRowIndices.Min(),
                EndRow = headerRowIndices.Max(),
                StartColumn = allHeaderCells.Min(c => c.ColumnIndex),
                EndColumn = allHeaderCells.Max(c => c.ColumnIndex),
                Confidence = 0.9,
                Type = headerRowIndices.Count > 1 ? HeaderType.MultiLevel : HeaderType.Simple
            });
        }

        return headers;
    }

    private double CalculateRowHeterogeneity(List<EnhancedCellEntity> cells, AnchorDetectionOptions options)
    {
        if (cells.Count == 0) return 0;

        var score = 0.0;
        var factors = 0;

        // Data type diversity
        var dataTypes = cells.Select(c => c.DataType).Distinct().Count();
        score += dataTypes / (double)Enum.GetValues<CellDataType>().Length;
        factors++;

        // Number format diversity
        if (options.ConsiderNumberFormats)
        {
            var formats = cells.Select(c => c.NumberFormatString).Distinct().Count();
            score += Math.Min(formats / 5.0, 1.0); // Cap at 5 different formats
            factors++;
        }

        // Style diversity
        if (options.ConsiderStyles)
        {
            var boldCount = cells.Count(c => c.Style?.IsBold ?? false);
            var colorCount = cells.Select(c => c.Style?.BackgroundColor).Distinct().Count();
            
            score += (boldCount > 0 && boldCount < cells.Count) ? 0.5 : 0;
            score += Math.Min(colorCount / 3.0, 1.0); // Cap at 3 different colors
            factors += 2;
        }

        // Text vs numbers ratio
        var textCells = cells.Count(c => c.DataType == CellDataType.String);
        var numericCells = cells.Count(c => c.DataType == CellDataType.Number);
        if (textCells > 0 && numericCells > 0)
        {
            var ratio = Math.Min(textCells, numericCells) / (double)Math.Max(textCells, numericCells);
            score += ratio;
            factors++;
        }

        return factors > 0 ? score / factors : 0;
    }

    private double CalculateColumnHeterogeneity(List<EnhancedCellEntity> cells, AnchorDetectionOptions options)
    {
        // Similar to row heterogeneity but with column-specific considerations
        return CalculateRowHeterogeneity(cells, options);
    }

    private bool HasSignificantChange(
        List<EnhancedCellEntity> prevCells, 
        List<EnhancedCellEntity> currentCells,
        AnchorDetectionOptions options)
    {
        // Check for significant structural changes
        
        // Major type shift (e.g., all numbers to all text)
        var prevTypes = prevCells.Select(c => c.DataType).Distinct().ToList();
        var currTypes = currentCells.Select(c => c.DataType).Distinct().ToList();
        
        if (!prevTypes.Intersect(currTypes).Any() && prevTypes.Count > 0 && currTypes.Count > 0)
            return true;

        // Significant style change
        if (options.ConsiderStyles)
        {
            var prevBold = prevCells.Count(c => c.Style?.IsBold ?? false);
            var currBold = currentCells.Count(c => c.Style?.IsBold ?? false);
            
            if ((prevBold == 0 && currBold > 0) || (prevBold > 0 && currBold == 0))
                return true;
        }

        return false;
    }

    private bool IsLikelyHeader(List<EnhancedCellEntity> cells)
    {
        if (cells.Count == 0) return false;

        // Headers typically have:
        // - Mostly string values
        // - Bold formatting
        // - No formulas
        // - Unique values

        var nonEmptyCells = cells.Where(c => c.DataType != CellDataType.Empty).ToList();
        if (nonEmptyCells.Count == 0) return false;

        var stringRatio = nonEmptyCells.Count(c => c.DataType == CellDataType.String) / (double)nonEmptyCells.Count;
        var hasBold = nonEmptyCells.Any(c => c.Style?.IsBold ?? false);
        var hasFormulas = nonEmptyCells.Any(c => c.DataType == CellDataType.Formula);
        var uniqueValues = nonEmptyCells.Select(c => c.FormattedValue).Distinct().Count();
        var uniqueRatio = uniqueValues / (double)nonEmptyCells.Count;

        return stringRatio > 0.7 && !hasFormulas && uniqueRatio > 0.8 && (hasBold || stringRatio > 0.9);
    }

    private HashSet<int> ExpandAnchors(HashSet<int> anchors, int k, int maxIndex)
    {
        var expanded = new HashSet<int>(anchors);
        
        foreach (var anchor in anchors)
        {
            // Add k neighbors on each side
            for (var i = Math.Max(0, anchor - k); i <= Math.Min(maxIndex, anchor + k); i++)
            {
                expanded.Add(i);
            }
        }

        return expanded;
    }

    private AnchorMetrics CalculateMetrics(
        HashSet<int> anchorRows,
        HashSet<int> anchorColumns,
        List<HeaderRegion> headerRegions,
        WorksheetContext worksheet,
        RowColumnAnalysis rowAnalysis,
        RowColumnAnalysis colAnalysis)
    {
        var totalAnchors = anchorRows.Count + anchorColumns.Count;
        var totalPossible = worksheet.Dimensions.LastRowIndex + worksheet.Dimensions.LastColumnIndex + 2;

        // Calculate average distance between anchors
        var avgDistance = 0.0;
        if (anchorRows.Count > 1)
        {
            var sortedRows = anchorRows.OrderBy(r => r).ToList();
            var rowDistances = new List<int>();
            for (var i = 1; i < sortedRows.Count; i++)
            {
                rowDistances.Add(sortedRows[i] - sortedRows[i - 1]);
            }
            avgDistance = rowDistances.Average();
        }

        return new AnchorMetrics
        {
            TotalAnchors = totalAnchors,
            AnchorDensity = totalPossible > 0 ? totalAnchors / (double)totalPossible : 0,
            AverageAnchorDistance = avgDistance,
            HeterogeneityScores = new Dictionary<string, double>
            {
                ["RowAverage"] = rowAnalysis.HeterogeneityScores.Values.DefaultIfEmpty(0).Average(),
                ["ColumnAverage"] = colAnalysis.HeterogeneityScores.Values.DefaultIfEmpty(0).Average(),
                ["RowMax"] = rowAnalysis.HeterogeneityScores.Values.DefaultIfEmpty(0).Max(),
                ["ColumnMax"] = colAnalysis.HeterogeneityScores.Values.DefaultIfEmpty(0).Max()
            }
        };
    }

    private List<CrossSheetPattern> DetectCrossSheetPatterns(
        IDictionary<string, StructuralAnchors> worksheetAnchors,
        WorkbookContext context)
    {
        var patterns = new List<CrossSheetPattern>();

        // Look for sheets with similar anchor patterns
        var sheetGroups = new Dictionary<string, List<string>>();
        
        foreach (var (sheetName, anchors) in worksheetAnchors)
        {
            var signature = GenerateAnchorSignature(anchors);
            if (!sheetGroups.ContainsKey(signature))
            {
                sheetGroups[signature] = new List<string>();
            }
            sheetGroups[signature].Add(sheetName);
        }

        // Create patterns for groups with multiple sheets
        foreach (var (signature, sheets) in sheetGroups.Where(g => g.Value.Count > 1))
        {
            patterns.Add(new CrossSheetPattern
            {
                PatternName = $"Pattern_{patterns.Count + 1}",
                AffectedSheets = sheets,
                Confidence = sheets.Count / (double)context.Worksheets.Count
            });
        }

        return patterns;
    }

    private string GenerateAnchorSignature(StructuralAnchors anchors)
    {
        // Create a simple signature based on anchor counts and positions
        var rowCount = anchors.AnchorRows.Count;
        var colCount = anchors.AnchorColumns.Count;
        var headerCount = anchors.HeaderRegions.Count;
        
        return $"R{rowCount}_C{colCount}_H{headerCount}";
    }

    private WorkbookAnchorMetrics CalculateGlobalMetrics(
        IDictionary<string, StructuralAnchors> worksheetAnchors,
        WorkbookContext context)
    {
        var allRowAnchors = worksheetAnchors.Values.SelectMany(a => a.AnchorRows).Count();
        var allColAnchors = worksheetAnchors.Values.SelectMany(a => a.AnchorColumns).Count();
        var totalAnchors = allRowAnchors + allColAnchors;
        
        var avgHeterogeneity = worksheetAnchors.Values
            .SelectMany(a => a.Metrics.HeterogeneityScores.Values)
            .DefaultIfEmpty(0)
            .Average();

        return new WorkbookAnchorMetrics
        {
            TotalAnchors = totalAnchors,
            TotalWorksheets = context.Worksheets.Count,
            AnchorDensity = totalAnchors / (double)context.Worksheets.Count,
            AverageAnchorDistance = worksheetAnchors.Values
                .Select(a => a.Metrics.AverageAnchorDistance)
                .Where(d => d > 0)
                .DefaultIfEmpty(0)
                .Average(),
            HeterogeneityScores = new Dictionary<string, double>
            {
                ["GlobalAverage"] = avgHeterogeneity
            },
            CrossSheetConsistency = CalculateCrossSheetConsistency(worksheetAnchors)
        };
    }

    private double CalculateCrossSheetConsistency(IDictionary<string, StructuralAnchors> worksheetAnchors)
    {
        if (worksheetAnchors.Count < 2) return 1.0;

        var signatures = worksheetAnchors.Values
            .Select(GenerateAnchorSignature)
            .ToList();

        var uniqueSignatures = signatures.Distinct().Count();
        
        // Consistency is higher when fewer unique patterns exist
        return 1.0 - (uniqueSignatures - 1.0) / (signatures.Count - 1.0);
    }

    private class RowColumnAnalysis
    {
        public HashSet<int> Anchors { get; } = new();
        public Dictionary<int, double> HeterogeneityScores { get; } = new();
    }
}