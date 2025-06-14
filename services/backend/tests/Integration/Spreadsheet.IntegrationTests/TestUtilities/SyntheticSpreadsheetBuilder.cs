// using System.Text;
// using Domain.Entities;
// using Domain.ValueObjects.Spreadsheet;
//
// namespace Spreadsheet.IntegrationTests.TestUtilities;
//
// /// <summary>
// /// Builder for creating synthetic spreadsheet data with configurable parameters.
// /// Supports multiple tables, various data types, and heterogeneous structures.
// /// </summary>
// public class SyntheticSpreadsheetBuilder
// {
//     private readonly List<SheetConfiguration> _sheets = new();
//     private readonly Random _random;
//     private int _seed = 42; // Default seed for reproducibility
//
//     public SyntheticSpreadsheetBuilder(int? seed = null)
//     {
//         _seed = seed ?? 42;
//         _random = new Random(_seed);
//     }
//
//     /// <summary>
//     /// Add a new sheet to the workbook with specified configuration.
//     /// </summary>
//     public SyntheticSpreadsheetBuilder AddSheet(string name, Action<SheetBuilder> configure)
//     {
//         var sheetBuilder = new SheetBuilder(name, _random);
//         configure(sheetBuilder);
//         _sheets.Add(sheetBuilder.Build());
//         return this;
//     }
//
//     /// <summary>
//     /// Build the synthetic workbook context.
//     /// </summary>
//     public WorkbookContext Build()
//     {
//         var worksheets = new List<WorksheetContext>();
//         
//         for (int i = 0; i < _sheets.Count; i++)
//         {
//             var sheet = _sheets[i];
//             var cells = GenerateCells(sheet);
//             
//             var worksheet = new WorksheetContext
//             {
//                 Name = sheet.Name,
//                 Index = i,
//                 Cells = cells,
//                 Dimensions = new WorksheetDimensions
//                 {
//                     TotalCells = cells.Count,
//                     NonEmptyCells = cells.Count(c => c.Value.DataType != CellDataType.Empty),
//                     MaxRow = cells.Any() ? cells.Max(c => c.Address.Row) : 0,
//                     MaxColumn = cells.Any() ? cells.Max(c => c.Address.Column) : 0
//                 }
//             };
//             
//             worksheets.Add(worksheet);
//         }
//
//         var totalCells = worksheets.Sum(w => w.Cells.Count);
//         var nonEmptyCells = worksheets.Sum(w => w.Cells.Count(c => c.Value.DataType != CellDataType.Empty));
//
//         return new WorkbookContext
//         {
//             FilePath = $"synthetic_workbook_{_seed}.xlsx",
//             Worksheets = worksheets,
//             Metadata = new WorkbookMetadata
//             {
//                 FileName = $"synthetic_workbook_{_seed}.xlsx",
//                 CreatedDate = DateTime.UtcNow,
//                 ModifiedDate = DateTime.UtcNow,
//                 Author = "SyntheticSpreadsheetBuilder",
//                 Properties = new Dictionary<string, string>
//                 {
//                     ["GeneratorSeed"] = _seed.ToString(),
//                     ["GeneratedAt"] = DateTime.UtcNow.ToString("O")
//                 }
//             },
//             Statistics = new WorkbookStatistics
//             {
//                 TotalCells = totalCells,
//                 NonEmptyCells = nonEmptyCells,
//                 EmptyCellPercentage = totalCells > 0 ? (double)(totalCells - nonEmptyCells) / totalCells : 0,
//                 TypeDistribution = CalculateTypeDistribution(worksheets)
//             }
//         };
//     }
//
//     /// <summary>
//     /// Build and save as CSV for easy inspection.
//     /// </summary>
//     public async Task<string> BuildAndSaveAsCsv(string directory)
//     {
//         var workbook = Build();
//         var csvFiles = new List<string>();
//
//         foreach (var worksheet in workbook.Worksheets)
//         {
//             var fileName = Path.Combine(directory, $"{worksheet.Name}.csv");
//             var csv = ConvertToCsv(worksheet);
//             await File.WriteAllTextAsync(fileName, csv);
//             csvFiles.Add(fileName);
//         }
//
//         return csvFiles.FirstOrDefault() ?? string.Empty;
//     }
//
//     private List<CellContext> GenerateCells(SheetConfiguration config)
//     {
//         var cells = new List<CellContext>();
//
//         foreach (var table in config.Tables)
//         {
//             cells.AddRange(GenerateTableCells(table, config));
//         }
//
//         // Add random sparse data if configured
//         if (config.SparseDataDensity > 0)
//         {
//             cells.AddRange(GenerateSparseData(config));
//         }
//
//         return cells;
//     }
//
//     private List<CellContext> GenerateTableCells(TableConfiguration table, SheetConfiguration sheet)
//     {
//         var cells = new List<CellContext>();
//
//         // Generate headers
//         for (int col = 0; col < table.Columns.Count; col++)
//         {
//             var column = table.Columns[col];
//             var address = new CellAddress(table.StartRow, table.StartColumn + col);
//             
//             cells.Add(new CellContext
//             {
//                 Address = address,
//                 Value = new CellValue
//                 {
//                     DataType = CellDataType.String,
//                     StringValue = column.Name
//                 },
//                 NumberFormatString = null,
//                 Style = table.HeaderStyle
//             });
//         }
//
//         // Generate data rows
//         for (int row = 0; row < table.RowCount; row++)
//         {
//             for (int col = 0; col < table.Columns.Count; col++)
//             {
//                 var column = table.Columns[col];
//                 var address = new CellAddress(table.StartRow + row + 1, table.StartColumn + col);
//                 
//                 // Apply empty cell probability
//                 if (_random.NextDouble() < sheet.EmptyCellProbability)
//                 {
//                     cells.Add(new CellContext
//                     {
//                         Address = address,
//                         Value = new CellValue { DataType = CellDataType.Empty },
//                         NumberFormatString = null
//                     });
//                     continue;
//                 }
//
//                 var cellValue = GenerateCellValue(column);
//                 cells.Add(new CellContext
//                 {
//                     Address = address,
//                     Value = cellValue,
//                     NumberFormatString = column.NumberFormat
//                 });
//             }
//         }
//
//         return cells;
//     }
//
//     private CellValue GenerateCellValue(ColumnConfiguration column)
//     {
//         return column.DataType switch
//         {
//             SyntheticDataType.Integer => new CellValue
//             {
//                 DataType = CellDataType.Number,
//                 NumberValue = _random.Next(column.MinValue ?? 0, column.MaxValue ?? 1000000)
//             },
//             SyntheticDataType.Decimal => new CellValue
//             {
//                 DataType = CellDataType.Number,
//                 NumberValue = _random.NextDouble() * ((column.MaxValue ?? 1000000) - (column.MinValue ?? 0)) + (column.MinValue ?? 0)
//             },
//             SyntheticDataType.Currency => new CellValue
//             {
//                 DataType = CellDataType.Number,
//                 NumberValue = Math.Round(_random.NextDouble() * ((column.MaxValue ?? 100000) - (column.MinValue ?? 0)) + (column.MinValue ?? 0), 2)
//             },
//             SyntheticDataType.Percentage => new CellValue
//             {
//                 DataType = CellDataType.Number,
//                 NumberValue = _random.NextDouble()
//             },
//             SyntheticDataType.Date => new CellValue
//             {
//                 DataType = CellDataType.Date,
//                 DateValue = DateTime.Now.AddDays(-_random.Next(0, 365))
//             },
//             SyntheticDataType.Text => new CellValue
//             {
//                 DataType = CellDataType.String,
//                 StringValue = GenerateRandomText(column)
//             },
//             SyntheticDataType.Boolean => new CellValue
//             {
//                 DataType = CellDataType.Boolean,
//                 BooleanValue = _random.Next(2) == 1
//             },
//             _ => new CellValue { DataType = CellDataType.Empty }
//         };
//     }
//
//     private string GenerateRandomText(ColumnConfiguration column)
//     {
//         if (column.PossibleValues?.Any() == true)
//         {
//             return column.PossibleValues[_random.Next(column.PossibleValues.Count)];
//         }
//
//         var words = new[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta" };
//         return words[_random.Next(words.Length)] + _random.Next(100, 999);
//     }
//
//     private List<CellContext> GenerateSparseData(SheetConfiguration config)
//     {
//         var cells = new List<CellContext>();
//         var sparseCount = (int)(config.MaxRow * config.MaxColumn * config.SparseDataDensity);
//
//         for (int i = 0; i < sparseCount; i++)
//         {
//             var row = _random.Next(1, config.MaxRow + 1);
//             var col = _random.Next(1, config.MaxColumn + 1);
//             
//             // Skip if cell would overlap with a table
//             if (config.Tables.Any(t => IsInTableRange(row, col, t)))
//                 continue;
//
//             var dataType = (SyntheticDataType)_random.Next(0, Enum.GetValues<SyntheticDataType>().Length);
//             var column = new ColumnConfiguration { Name = "Sparse", DataType = dataType };
//             
//             cells.Add(new CellContext
//             {
//                 Address = new CellAddress(row, col),
//                 Value = GenerateCellValue(column),
//                 NumberFormatString = GetDefaultFormat(dataType)
//             });
//         }
//
//         return cells;
//     }
//
//     private bool IsInTableRange(int row, int col, TableConfiguration table)
//     {
//         return row >= table.StartRow && 
//                row < table.StartRow + table.RowCount + 1 && // +1 for header
//                col >= table.StartColumn && 
//                col < table.StartColumn + table.Columns.Count;
//     }
//
//     private string? GetDefaultFormat(SyntheticDataType dataType)
//     {
//         return dataType switch
//         {
//             SyntheticDataType.Currency => "$#,##0.00",
//             SyntheticDataType.Percentage => "0.00%",
//             SyntheticDataType.Date => "yyyy-mm-dd",
//             _ => null
//         };
//     }
//
//     private Dictionary<CellDataType, int> CalculateTypeDistribution(List<WorksheetContext> worksheets)
//     {
//         var distribution = new Dictionary<CellDataType, int>();
//         
//         foreach (CellDataType type in Enum.GetValues<CellDataType>())
//         {
//             distribution[type] = 0;
//         }
//
//         foreach (var worksheet in worksheets)
//         {
//             foreach (var cell in worksheet.Cells)
//             {
//                 distribution[cell.Value.DataType]++;
//             }
//         }
//
//         return distribution;
//     }
//
//     private string ConvertToCsv(WorksheetContext worksheet)
//     {
//         if (!worksheet.Cells.Any())
//             return string.Empty;
//
//         var maxRow = worksheet.Cells.Max(c => c.Address.Row);
//         var maxCol = worksheet.Cells.Max(c => c.Address.Column);
//         
//         var grid = new string?[maxRow + 1, maxCol + 1];
//         
//         foreach (var cell in worksheet.Cells)
//         {
//             grid[cell.Address.Row, cell.Address.Column] = cell.GetStringValue();
//         }
//
//         var csv = new StringBuilder();
//         
//         for (int row = 1; row <= maxRow; row++)
//         {
//             var rowValues = new List<string>();
//             for (int col = 1; col <= maxCol; col++)
//             {
//                 var value = grid[row, col] ?? "";
//                 // Escape quotes and wrap in quotes if contains comma
//                 if (value.Contains(',') || value.Contains('"'))
//                 {
//                     value = $"\"{value.Replace("\"", "\"\"")}\"";
//                 }
//                 rowValues.Add(value);
//             }
//             csv.AppendLine(string.Join(",", rowValues));
//         }
//
//         return csv.ToString();
//     }
// }
//
// /// <summary>
// /// Builder for configuring individual sheets.
// /// </summary>
// public class SheetBuilder
// {
//     private readonly SheetConfiguration _config;
//     private readonly Random _random;
//
//     internal SheetBuilder(string name, Random random)
//     {
//         _config = new SheetConfiguration { Name = name };
//         _random = random;
//     }
//
//     public SheetBuilder WithDimensions(int maxRow, int maxColumn)
//     {
//         _config.MaxRow = maxRow;
//         _config.MaxColumn = maxColumn;
//         return this;
//     }
//
//     public SheetBuilder WithEmptyCellProbability(double probability)
//     {
//         _config.EmptyCellProbability = Math.Clamp(probability, 0, 1);
//         return this;
//     }
//
//     public SheetBuilder WithSparseData(double density)
//     {
//         _config.SparseDataDensity = Math.Clamp(density, 0, 1);
//         return this;
//     }
//
//     public SheetBuilder AddTable(Action<TableBuilder> configure)
//     {
//         var tableBuilder = new TableBuilder(_random);
//         configure(tableBuilder);
//         _config.Tables.Add(tableBuilder.Build());
//         return this;
//     }
//
//     internal SheetConfiguration Build() => _config;
// }
//
// /// <summary>
// /// Builder for configuring tables within sheets.
// /// </summary>
// public class TableBuilder
// {
//     private readonly TableConfiguration _config = new();
//     private readonly Random _random;
//
//     internal TableBuilder(Random random)
//     {
//         _random = random;
//     }
//
//     public TableBuilder At(int row, int column)
//     {
//         _config.StartRow = row;
//         _config.StartColumn = column;
//         return this;
//     }
//
//     public TableBuilder WithRows(int count)
//     {
//         _config.RowCount = count;
//         return this;
//     }
//
//     public TableBuilder WithHeaderStyle(string style)
//     {
//         _config.HeaderStyle = style;
//         return this;
//     }
//
//     public TableBuilder AddColumn(string name, SyntheticDataType dataType, Action<ColumnBuilder>? configure = null)
//     {
//         var columnBuilder = new ColumnBuilder(name, dataType);
//         configure?.Invoke(columnBuilder);
//         _config.Columns.Add(columnBuilder.Build());
//         return this;
//     }
//
//     internal TableConfiguration Build() => _config;
// }
//
// /// <summary>
// /// Builder for configuring table columns.
// /// </summary>
// public class ColumnBuilder
// {
//     private readonly ColumnConfiguration _config;
//
//     internal ColumnBuilder(string name, SyntheticDataType dataType)
//     {
//         _config = new ColumnConfiguration { Name = name, DataType = dataType };
//     }
//
//     public ColumnBuilder WithRange(int min, int max)
//     {
//         _config.MinValue = min;
//         _config.MaxValue = max;
//         return this;
//     }
//
//     public ColumnBuilder WithFormat(string numberFormat)
//     {
//         _config.NumberFormat = numberFormat;
//         return this;
//     }
//
//     public ColumnBuilder WithPossibleValues(params string[] values)
//     {
//         _config.PossibleValues = values.ToList();
//         return this;
//     }
//
//     internal ColumnConfiguration Build() => _config;
// }
//
// // Configuration classes
// internal class SheetConfiguration
// {
//     public string Name { get; set; } = "Sheet1";
//     public int MaxRow { get; set; } = 100;
//     public int MaxColumn { get; set; } = 26;
//     public double EmptyCellProbability { get; set; } = 0.1;
//     public double SparseDataDensity { get; set; } = 0;
//     public List<TableConfiguration> Tables { get; set; } = new();
// }
//
// internal class TableConfiguration
// {
//     public int StartRow { get; set; } = 1;
//     public int StartColumn { get; set; } = 1;
//     public int RowCount { get; set; } = 10;
//     public string? HeaderStyle { get; set; }
//     public List<ColumnConfiguration> Columns { get; set; } = new();
// }
//
// internal class ColumnConfiguration
// {
//     public string Name { get; set; } = "";
//     public SyntheticDataType DataType { get; set; }
//     public string? NumberFormat { get; set; }
//     public int? MinValue { get; set; }
//     public int? MaxValue { get; set; }
//     public List<string>? PossibleValues { get; set; }
// }
//
// public enum SyntheticDataType
// {
//     Integer,
//     Decimal,
//     Currency,
//     Percentage,
//     Date,
//     Text,
//     Boolean
// }