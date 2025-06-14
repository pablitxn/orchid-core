using Application.Interfaces.Spreadsheet;
using Application.UseCases.Spreadsheet.Compression;
using Domain.ValueObjects.Spreadsheet;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace Application.Tests.UseCases.Spreadsheet.Compression;

public class InvertedIndexTranslatorTests
{
    private readonly IInvertedIndexTranslator _translator = new InvertedIndexTranslator();
    
    [Fact]
    public async Task ToInvertedIndex_EmptyCells_NotIncludedInOutput()
    {
        // Arrange
        var worksheet = new WorksheetContext
        {
            Name = "Sheet1",
            Cells = new[]
            {
                new CellData { Address = new CellAddress("A1"), Value = "Invoice#" },
                new CellData { Address = new CellAddress("A2"), Value = null },
                new CellData { Address = new CellAddress("A3"), Value = "" },
                new CellData { Address = new CellAddress("A4"), Value = "   " },
                new CellData { Address = new CellAddress("A5"), Value = "INV-001" }
            },
            Dimensions = (5, 1)
        };
        
        // Act
        var result = await _translator.ToInvertedIndexAsync(worksheet);
        var parsed = JsonDocument.Parse(result);
        
        // Assert
        var invoiceProperty = parsed.RootElement.GetProperty("Invoice#");
        if (invoiceProperty.ValueKind == JsonValueKind.String)
        {
            invoiceProperty.GetString().Should().Be("A1");
        }
        else
        {
            var addresses = invoiceProperty.EnumerateArray().Select(e => e.GetString()).ToArray();
            addresses.Should().Contain("A1");
        }
        
        var invProperty = parsed.RootElement.GetProperty("INV-001");
        if (invProperty.ValueKind == JsonValueKind.String)
        {
            invProperty.GetString().Should().Be("A5");
        }
        else
        {
            var addresses = invProperty.EnumerateArray().Select(e => e.GetString()).ToArray();
            addresses.Should().Contain("A5");
        }
        
        parsed.RootElement.EnumerateObject().Should().HaveCount(2);
    }
    
    [Fact]
    public async Task ToInvertedIndex_DuplicateValues_GroupedTogether()
    {
        // Arrange
        var worksheet = new WorksheetContext
        {
            Name = "Sheet1",
            Cells = new[]
            {
                new CellData { Address = new CellAddress("A1"), Value = "Invoice#" },
                new CellData { Address = new CellAddress("A2"), Value = "Invoice#" },
                new CellData { Address = new CellAddress("A7"), Value = "Invoice#" }
            },
            Dimensions = (7, 1)
        };
        
        var options = new InvertedIndexOptions { OptimizeRanges = false };
        
        // Act
        var result = await _translator.ToInvertedIndexAsync(worksheet, options);
        var parsed = JsonDocument.Parse(result);
        
        // Assert
        var addresses = parsed.RootElement.GetProperty("Invoice#");
        addresses.GetArrayLength().Should().Be(3);
        addresses[0].GetString().Should().Be("A1");
        addresses[1].GetString().Should().Be("A2");
        addresses[2].GetString().Should().Be("A7");
    }
    
    [Fact]
    public async Task ToInvertedIndex_ContiguousRange_OptimizedToRangeNotation()
    {
        // Arrange
        var cells = new List<CellData>();
        for (int i = 0; i < 10; i++)
        {
            cells.Add(new CellData 
            { 
                Address = new CellAddress(i, 0), 
                Value = "Product" 
            });
        }
        
        var worksheet = new WorksheetContext
        {
            Name = "Sheet1",
            Cells = cells,
            Dimensions = (10, 1)
        };
        
        // Act
        var result = await _translator.ToInvertedIndexAsync(worksheet);
        var parsed = JsonDocument.Parse(result);
        
        // Assert
        parsed.RootElement.GetProperty("Product").GetString().Should().Be("A1:A10");
    }
    
    [Fact]
    public async Task ToInvertedIndex_WithNumberFormat_IncludesFormatInKey()
    {
        // Arrange
        var worksheet = new WorksheetContext
        {
            Name = "Sheet1",
            Cells = new[]
            {
                new CellData 
                { 
                    Address = new CellAddress("A1"), 
                    Value = 1000.5, 
                    NumberFormatString = "$#,##0.00" 
                },
                new CellData 
                { 
                    Address = new CellAddress("A2"), 
                    Value = 1000.5, 
                    NumberFormatString = "0.00%" 
                }
            },
            Dimensions = (2, 1)
        };
        
        // Act
        var result = await _translator.ToInvertedIndexAsync(worksheet);
        var parsed = JsonDocument.Parse(result);
        
        // Assert
        parsed.RootElement.EnumerateObject().Should().HaveCount(2);
        
        var currency = parsed.RootElement.GetProperty("1000.5|$#,##0.00");
        if (currency.ValueKind == JsonValueKind.String)
        {
            currency.GetString().Should().Be("A1");
        }
        else
        {
            var addresses = currency.EnumerateArray().Select(e => e.GetString()).ToArray();
            addresses.Should().Contain("A1");
        }
        
        var percent = parsed.RootElement.GetProperty("1000.5|0.00%");
        if (percent.ValueKind == JsonValueKind.String)
        {
            percent.GetString().Should().Be("A2");
        }
        else
        {
            var addresses = percent.EnumerateArray().Select(e => e.GetString()).ToArray();
            addresses.Should().Contain("A2");
        }
    }
    
    [Fact]
    public async Task ToInvertedIndex_HorizontalRange_OptimizedCorrectly()
    {
        // Arrange
        var cells = new List<CellData>();
        for (int i = 0; i < 5; i++)
        {
            cells.Add(new CellData 
            { 
                Address = new CellAddress(0, i), 
                Value = "Header" 
            });
        }
        
        var worksheet = new WorksheetContext
        {
            Name = "Sheet1",
            Cells = cells,
            Dimensions = (1, 5)
        };
        
        // Act
        var result = await _translator.ToInvertedIndexAsync(worksheet);
        var parsed = JsonDocument.Parse(result);
        
        // Assert
        parsed.RootElement.GetProperty("Header").GetString().Should().Be("A1:E1");
    }
    
    [Fact]
    public async Task ToInvertedIndex_MixedRangesAndSingles_HandledCorrectly()
    {
        // Arrange
        var worksheet = new WorksheetContext
        {
            Name = "Sheet1",
            Cells = new[]
            {
                new CellData { Address = new CellAddress("A1"), Value = "Item" },
                new CellData { Address = new CellAddress("A2"), Value = "Item" },
                new CellData { Address = new CellAddress("A3"), Value = "Item" },
                new CellData { Address = new CellAddress("A5"), Value = "Item" },
                new CellData { Address = new CellAddress("B1"), Value = "Item" }
            },
            Dimensions = (5, 2)
        };
        
        // Act
        var result = await _translator.ToInvertedIndexAsync(worksheet);
        var parsed = JsonDocument.Parse(result);
        
        // Assert
        var itemValue = parsed.RootElement.GetProperty("Item");
        itemValue.ValueKind.Should().Be(JsonValueKind.Array);
        
        var items = itemValue.EnumerateArray().Select(e => e.GetString()).ToList();
        items.Should().Contain("A1:A3");
        items.Should().Contain("A5");
        items.Should().Contain("B1");
    }
}