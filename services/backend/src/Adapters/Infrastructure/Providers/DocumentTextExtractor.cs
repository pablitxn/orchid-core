using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Application.Interfaces;
using Aspose.Cells;

namespace Infrastructure.Providers;

/// <summary>
///     Parses common document formats and extracts plain text.
/// </summary>
public class DocumentTextExtractor : IDocumentTextExtractor
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".md",
        ".csv",
        ".json",
        ".xml",
        ".html",
        ".htm",
        ".log",
        ".yaml",
        ".yml"
    };

    public async Task<string> ExtractAsync(Stream stream, string extension,
        CancellationToken cancellationToken = default)
    {
        extension = extension.ToLowerInvariant();
        if (extension == ".odt")
            try
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, false);
                var entry = archive.GetEntry("content.xml");
                if (entry == null)
                    return string.Empty;
                using var entryStream = entry.Open();
                var xdoc = XDocument.Load(entryStream);
                XNamespace textNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
                var paragraphsList = xdoc.Descendants(textNs + "p")
                    .Select(p => p.Value.Trim())
                    .Where(p => !string.IsNullOrEmpty(p));
                return string.Join("\n\n", paragraphsList);
            }
            catch
            {
                // Gracefully handle malformed or corrupted .odt files
                return string.Empty;
            }

        if (extension == ".xlsx")
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            var workbook = new Workbook(ms);
            var sb = new StringBuilder();
            foreach (var sheet in workbook.Worksheets)
            {
                for (var r = 0; r <= sheet.Cells.MaxDataRow; r++)
                {
                    var rowValues = new List<string>();
                    for (var c = 0; c <= sheet.Cells.MaxDataColumn; c++)
                    {
                        var cellValue = sheet.Cells[r, c].StringValue;
                        if (!string.IsNullOrWhiteSpace(cellValue))
                            rowValues.Add(cellValue.Trim());
                    }

                    if (rowValues.Count > 0)
                        sb.AppendLine(string.Join(" ", rowValues));
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        if (!TextExtensions.Contains(extension))
            throw new NotSupportedException($"Unsupported file extension '{extension}' for text extraction.");

        using var reader = new StreamReader(stream, leaveOpen: false);
        return await reader.ReadToEndAsync();
    }
}