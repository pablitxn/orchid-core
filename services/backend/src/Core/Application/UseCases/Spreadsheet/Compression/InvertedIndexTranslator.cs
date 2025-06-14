using System.Text;
using System.Text.Json;
using Application.Interfaces.Spreadsheet;
using Domain.ValueObjects.Spreadsheet;

namespace Application.UseCases.Spreadsheet.Compression;

public sealed class InvertedIndexTranslator : IInvertedIndexTranslator
{
    public Task<string> ToInvertedIndexAsync(WorksheetContext worksheet, InvertedIndexOptions? options = null)
    {
        options ??= new InvertedIndexOptions();
        
        var index = new Dictionary<string, List<CellAddress>>();
        
        // Group cells by value and format
        foreach (var cell in worksheet.Cells)
        {
            if (cell.IsEmpty) continue; // Skip empty cells
            
            var key = cell.GetStringValue();
            if (options.IncludeFormats && !string.IsNullOrEmpty(cell.NumberFormatString))
            {
                key = $"{key}|{cell.NumberFormatString}";
            }
            
            if (!index.ContainsKey(key))
            {
                index[key] = new List<CellAddress>();
            }
            
            index[key].Add(cell.Address);
        }
        
        // Convert to optimized format
        var result = new Dictionary<string, object>();
        
        foreach (var (key, addresses) in index)
        {
            if (options.OptimizeRanges && addresses.Count >= options.RangeThreshold)
            {
                var optimized = OptimizeAddresses(addresses);
                result[key] = optimized;
            }
            else
            {
                result[key] = addresses.Select(a => a.A1Reference).ToArray();
            }
        }
        
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = null
        };
        
        return Task.FromResult(JsonSerializer.Serialize(result, jsonOptions));
    }
    
    private static object OptimizeAddresses(List<CellAddress> addresses)
    {
        addresses.Sort((a, b) => 
        {
            var colCompare = a.Column.CompareTo(b.Column);
            return colCompare != 0 ? colCompare : a.Row.CompareTo(b.Row);
        });
        
        var results = new List<string>();
        var i = 0;
        
        while (i < addresses.Count)
        {
            var start = addresses[i];
            var end = start;
            
            // Check for vertical range (same column)
            if (i + 1 < addresses.Count && addresses[i + 1].Column == start.Column)
            {
                var j = i + 1;
                while (j < addresses.Count && 
                       addresses[j].Column == start.Column && 
                       addresses[j].Row == addresses[j - 1].Row + 1)
                {
                    end = addresses[j];
                    j++;
                }
                
                if (j - i >= 3) // Found a range
                {
                    results.Add($"{start.A1Reference}:{end.A1Reference}");
                    i = j;
                    continue;
                }
            }
            
            // Check for horizontal range (same row)
            if (i + 1 < addresses.Count && addresses[i + 1].Row == start.Row)
            {
                var j = i + 1;
                while (j < addresses.Count && 
                       addresses[j].Row == start.Row && 
                       addresses[j].Column == addresses[j - 1].Column + 1)
                {
                    end = addresses[j];
                    j++;
                }
                
                if (j - i >= 3) // Found a range
                {
                    results.Add($"{start.A1Reference}:{end.A1Reference}");
                    i = j;
                    continue;
                }
            }
            
            // Single cell
            results.Add(start.A1Reference);
            i++;
        }
        
        return results.Count == 1 ? results[0] : results.ToArray();
    }
}