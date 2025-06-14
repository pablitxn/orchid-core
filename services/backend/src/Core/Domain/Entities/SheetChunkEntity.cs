using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace Domain.Entities;

/// <summary>
///     Represents a chunk of a spreadsheet document with text and embedding.
/// </summary>
public class SheetChunkEntity
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public DocumentEntity Document { get; set; } = null!;

    public string SheetName { get; set; } = string.Empty;

    public int StartRow { get; set; }

    public int EndRow { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>
    ///     Embedding vector for semantic retrieval of this chunk.
    /// </summary>
    [Column(TypeName = "vector(1536)")]
    public Vector? Embedding { get; set; }
}