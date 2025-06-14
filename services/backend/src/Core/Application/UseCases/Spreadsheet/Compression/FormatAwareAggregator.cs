using Application.Interfaces.Spreadsheet;
using System.Threading;
using Domain.ValueObjects.Spreadsheet;

namespace Application.UseCases.Spreadsheet.Compression;

public sealed class FormatAwareAggregator : IFormatAwareAggregator
{
    private static readonly ITypeRecognizer[] DefaultRecognizers = 
    {
        new DateTypeRecognizer(),
        new PercentageTypeRecognizer(),
        new CurrencyTypeRecognizer(),
        new ScientificTypeRecognizer(),
        new TimeTypeRecognizer(),
        new FractionTypeRecognizer(),
        new AccountingTypeRecognizer(),
        new BooleanTypeRecognizer(),
        new NumberTypeRecognizer()
    };
    
    public Task<AggregatedWorksheet> AggregateAsync(WorksheetContext worksheet, FormatAggregationOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Check for cancellation before starting aggregation
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new FormatAggregationOptions();
        
        var recognizers = options.TypeRecognizers.Count > 0 
            ? options.TypeRecognizers.ToArray() 
            : DefaultRecognizers;
        
        var regions = new List<AggregatedRegion>();
        var processedCells = new HashSet<CellAddress>();
        
        // Sort cells by row then column for sequential processing
        var sortedCells = worksheet.Cells
            .Where(c => !c.IsEmpty)
            .OrderBy(c => c.Address.Row)
            .ThenBy(c => c.Address.Column)
            .ToList();
        
        var originalCellCount = sortedCells.Count;
        
        foreach (var cell in sortedCells)
        {
            if (processedCells.Contains(cell.Address)) continue;
            
            var typeInfo = DetermineType(cell, recognizers, options.EnableTypeRecognition);
            if (typeInfo == null) continue;
            
            var adjacentCells = FindAdjacentCellsWithSameType(
                sortedCells, 
                cell, 
                typeInfo, 
                processedCells,
                recognizers,
                options);
            
            if (adjacentCells.Count >= options.MinGroupSize)
            {
                var region = CreateRegion(adjacentCells, typeInfo);
                regions.Add(region);
                
                foreach (var adjCell in adjacentCells)
                {
                    processedCells.Add(adjCell.Address);
                }
            }
        }
        
        // Add individual cells that weren't aggregated
        foreach (var cell in sortedCells)
        {
            if (!processedCells.Contains(cell.Address))
            {
                var typeInfo = DetermineType(cell, recognizers, options.EnableTypeRecognition);
                regions.Add(new AggregatedRegion
                {
                    StartAddress = cell.Address,
                    EndAddress = cell.Address,
                    TypeToken = typeInfo?.Token ?? cell.GetStringValue(),
                    FormatString = cell.NumberFormatString,
                    CellCount = 1
                });
            }
        }
        
        var aggregatedCellCount = regions.Sum(r => r.CellCount > 1 ? 1 : r.CellCount);
        var compressionRatio = originalCellCount > 0 ? (double)originalCellCount / aggregatedCellCount : 1.0;
        
        var result = new AggregatedWorksheet
        {
            Name = worksheet.Name,
            Regions = regions,
            CompressionRatio = compressionRatio
        };
        
        return Task.FromResult(result);
    }
    
    private static TypeInfo? DetermineType(CellData cell, ITypeRecognizer[] recognizers, bool enableTypeRecognition)
    {
        // First check format string
        if (!string.IsNullOrEmpty(cell.NumberFormatString))
        {
            foreach (var recognizer in recognizers)
            {
                if (recognizer.CanRecognize(cell.Value, cell.NumberFormatString))
                {
                    return new TypeInfo 
                    { 
                        Recognizer = recognizer, 
                        Token = recognizer.GetTypeToken(),
                        FormatString = cell.NumberFormatString
                    };
                }
            }
        }
        
        // Then try type recognition if enabled
        if (enableTypeRecognition)
        {
            foreach (var recognizer in recognizers)
            {
                if (recognizer.CanRecognize(cell.Value, null))
                {
                    return new TypeInfo 
                    { 
                        Recognizer = recognizer, 
                        Token = recognizer.GetTypeToken(),
                        FormatString = null
                    };
                }
            }
        }
        
        return null;
    }
    
    private static List<CellData> FindAdjacentCellsWithSameType(
        List<CellData> allCells,
        CellData startCell,
        TypeInfo typeInfo,
        HashSet<CellAddress> processedCells,
        ITypeRecognizer[] recognizers,
        FormatAggregationOptions options)
    {
        // Create lookup dictionary for O(1) cell access
        var cellLookup = allCells.ToDictionary(c => c.Address, c => c);
        
        var result = new List<CellData> { startCell };
        var toCheck = new Queue<CellData>();
        toCheck.Enqueue(startCell);
        var checkedAddresses = new HashSet<CellAddress> { startCell.Address };
        
        while (toCheck.Count > 0)
        {
            var current = toCheck.Dequeue();
            
            // Check all adjacent cells (up, down, left, right) with bounds checking to avoid invalid addresses
            var directions = new (int dRow, int dCol)[]
            {
                (-1, 0),
                (1, 0),
                (0, -1),
                (0, 1)
            };
            
            foreach (var (dRow, dCol) in directions)
            {
                var newRow = current.Address.Row + dRow;
                var newCol = current.Address.Column + dCol;
                // Skip out-of-bounds indices (row/col must be ≥ 0)
                if (newRow < 0 || newCol < 0)
                    continue;

                var adjAddr = new CellAddress(newRow, newCol);
                if (checkedAddresses.Contains(adjAddr) || processedCells.Contains(adjAddr))
                    continue;

                if (!cellLookup.TryGetValue(adjAddr, out var adjCell) || adjCell.IsEmpty)
                    continue;

                var adjType = DetermineType(adjCell, recognizers, options.EnableTypeRecognition);
                if (adjType != null && IsSameType(typeInfo, adjType))
                {
                    result.Add(adjCell);
                    toCheck.Enqueue(adjCell);
                    checkedAddresses.Add(adjAddr);
                }
            }
        }
        
        return result;
    }
    
    private static bool IsSameType(TypeInfo type1, TypeInfo type2)
    {
        // If both have format strings, they must match
        if (!string.IsNullOrEmpty(type1.FormatString) && !string.IsNullOrEmpty(type2.FormatString))
        {
            return type1.FormatString == type2.FormatString;
        }
        
        // Otherwise, check if they're recognized as the same type
        return type1.Recognizer.TypeName == type2.Recognizer.TypeName;
    }
    
    private static AggregatedRegion CreateRegion(List<CellData> cells, TypeInfo typeInfo)
    {
        var minRow = cells.Min(c => c.Address.Row);
        var maxRow = cells.Max(c => c.Address.Row);
        var minCol = cells.Min(c => c.Address.Column);
        var maxCol = cells.Max(c => c.Address.Column);
        
        return new AggregatedRegion
        {
            StartAddress = new CellAddress(minRow, minCol),
            EndAddress = new CellAddress(maxRow, maxCol),
            TypeToken = typeInfo.Token,
            FormatString = typeInfo.FormatString,
            CellCount = cells.Count
        };
    }
    
    private class TypeInfo
    {
        public ITypeRecognizer Recognizer { get; init; } = null!;
        public string Token { get; init; } = string.Empty;
        public string? FormatString { get; init; }
    }
}