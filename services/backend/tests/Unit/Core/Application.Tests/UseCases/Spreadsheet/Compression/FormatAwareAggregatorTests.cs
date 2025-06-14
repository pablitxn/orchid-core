using Application.Interfaces.Spreadsheet;
using Application.UseCases.Spreadsheet.Compression;
using Domain.ValueObjects.Spreadsheet;
using FluentAssertions;
using Xunit;

namespace Application.Tests.UseCases.Spreadsheet.Compression;

public class FormatAwareAggregatorTests
{
    private readonly IFormatAwareAggregator _aggregator = new FormatAwareAggregator();
    
    [Fact]
    public async Task Aggregate_AdjacentCellsWithSameFormat_CollapsedIntoRegion()
    {
        // Arrange
        var worksheet = new WorksheetContext
        {
            Name = "Sheet1",
            Cells = new[]
            {
                new CellData { Address = new CellAddress(0, 0), Value = 1000.50m, NumberFormatString = "$#,##0.00" },
                new CellData { Address = new CellAddress(0, 1), Value = 2500.75m, NumberFormatString = "$#,##0.00" },
                new CellData { Address = new CellAddress(0, 2), Value = 3200.00m, NumberFormatString = "$#,##0.00" }
            },
            Dimensions = (1, 3)
        };
        
        // Act
        var result = await _aggregator.AggregateAsync(worksheet);
        
        // Assert
        result.Regions.Should().HaveCount(1);
        var region = result.Regions[0];
        region.StartAddress.Should().Be(new CellAddress(0, 0));
        region.EndAddress.Should().Be(new CellAddress(0, 2));
        region.TypeToken.Should().Be("Currency");
        region.CellCount.Should().Be(3);
    }
    
    [Fact]
    public async Task Aggregate_DifferentFormats_SeparateRegions()
    {
        // Arrange
        var worksheet = new WorksheetContext
        {
            Name = "Sheet1",
            Cells = new[]
            {
                new CellData { Address = new CellAddress(0, 0), Value = 0.75m, NumberFormatString = "0.00%" },
                new CellData { Address = new CellAddress(0, 1), Value = 1000m, NumberFormatString = "$#,##0.00" },
                new CellData { Address = new CellAddress(0, 2), Value = 0.85m, NumberFormatString = "0.00%" }
            },
            Dimensions = (1, 3)
        };
        
        // Act
        var result = await _aggregator.AggregateAsync(worksheet);
        
        // Assert
        result.Regions.Should().HaveCount(3); // Each cell is a separate region
        result.Regions[0].TypeToken.Should().Be("0.00%");
        result.Regions[1].TypeToken.Should().Be("Currency");
        result.Regions[2].TypeToken.Should().Be("0.00%");
    }
    
    [Fact]
    public async Task Aggregate_TypeRecognition_DatePattern()
    {
        // Arrange
        var worksheet = new WorksheetContext
        {
            Name = "Sheet1",
            Cells = new[]
            {
                new CellData { Address = new CellAddress(0, 0), Value = "2024-01-15" },
                new CellData { Address = new CellAddress(1, 0), Value = "2024-01-16" },
                new CellData { Address = new CellAddress(2, 0), Value = "2024-01-17" }
            },
            Dimensions = (3, 1)
        };
        
        var options = new FormatAggregationOptions { EnableTypeRecognition = true };
        
        // Act
        var result = await _aggregator.AggregateAsync(worksheet, options);
        
        // Assert
        result.Regions.Should().HaveCount(1);
        result.Regions[0].TypeToken.Should().Be("yyyy-mm-dd");
        result.Regions[0].CellCount.Should().Be(3);
    }
    
    [Fact]
    public async Task Aggregate_TypeRecognition_PercentagePattern()
    {
        // Arrange
        var worksheet = new WorksheetContext
        {
            Name = "Sheet1",
            Cells = new[]
            {
                new CellData { Address = new CellAddress(0, 0), Value = "75%" },
                new CellData { Address = new CellAddress(0, 1), Value = "80.5%" },
                new CellData { Address = new CellAddress(1, 0), Value = "90%" }
            },
            Dimensions = (2, 2)
        };
        
        var options = new FormatAggregationOptions { EnableTypeRecognition = true };
        
        // Act
        var result = await _aggregator.AggregateAsync(worksheet, options);
        
        // Assert
        var percentRegion = result.Regions.FirstOrDefault(r => r.TypeToken == "0.00%");
        percentRegion.Should().NotBeNull();
        percentRegion!.CellCount.Should().Be(3);
    }
    
    [Fact]
    public async Task Aggregate_CompressionRatio_CalculatedCorrectly()
    {
        // Arrange
        var cells = new List<CellData>();
        for (int i = 0; i < 20; i++)
        {
            cells.Add(new CellData 
            { 
                Address = new CellAddress(i, 0), 
                Value = 1000 + i, 
                NumberFormatString = "$#,##0.00" 
            });
        }
        
        var worksheet = new WorksheetContext
        {
            Name = "Sheet1",
            Cells = cells,
            Dimensions = (20, 1)
        };
        
        // Act
        var result = await _aggregator.AggregateAsync(worksheet);
        
        // Assert
        result.CompressionRatio.Should().BeGreaterThan(1.0);
        result.Regions.Count.Should().BeLessThan(20);
    }
    
    [Fact]
    public async Task Aggregate_MinGroupSize_RespectedForAggregation()
    {
        // Arrange
        var worksheet = new WorksheetContext
        {
            Name = "Sheet1",
            Cells = new[]
            {
                new CellData { Address = new CellAddress(0, 0), Value = 100m, NumberFormatString = "$#,##0.00" },
                new CellData { Address = new CellAddress(0, 1), Value = 200m, NumberFormatString = "$#,##0.00" },
                new CellData { Address = new CellAddress(0, 3), Value = 300m, NumberFormatString = "$#,##0.00" }
            },
            Dimensions = (1, 4)
        };
        
        var options = new FormatAggregationOptions { MinGroupSize = 3 };
        
        // Act
        var result = await _aggregator.AggregateAsync(worksheet, options);
        
        // Assert
        // Should not aggregate the first two cells since they don't meet MinGroupSize
        result.Regions.Should().HaveCount(3);
        result.Regions.All(r => r.CellCount == 1).Should().BeTrue();
    }
    
    [Fact]
    public async Task Aggregate_CustomTypeRecognizer_Used()
    {
        // Arrange
        var customRecognizer = new CustomTestTypeRecognizer();
        var worksheet = new WorksheetContext
        {
            Name = "Sheet1",
            Cells = new[]
            {
                new CellData { Address = new CellAddress(0, 0), Value = "TEST-001" },
                new CellData { Address = new CellAddress(0, 1), Value = "TEST-002" }
            },
            Dimensions = (1, 2)
        };
        
        var options = new FormatAggregationOptions 
        { 
            EnableTypeRecognition = true,
            TypeRecognizers = { customRecognizer }
        };
        
        // Act
        var result = await _aggregator.AggregateAsync(worksheet, options);
        
        // Assert
        result.Regions.Should().HaveCount(1);
        result.Regions[0].TypeToken.Should().Be("TEST-XXX");
    }
    
    private class CustomTestTypeRecognizer : ITypeRecognizer
    {
        public string TypeName => "TestType";
        
        public bool CanRecognize(object? value, string? formatString)
        {
            return value?.ToString()?.StartsWith("TEST-") == true;
        }
        
        public string GetTypeToken() => "TEST-XXX";
    }
}