using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;
using Pgvector;

namespace Domain.Entities;

/// <summary>
///     Represents an uploaded document in a chat session.
/// </summary>
public class DocumentEntity
{
    public Guid Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string DocumentPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DocumentEnum Enum { get; set; } = DocumentEnum.Attachment;

    /// <summary>
    ///     Embedding vector for semantic retrieval.
    /// </summary>
    [Column(TypeName = "vector(1536)")]
    public Vector? Embedding { get; set; }

    /// <summary>
    ///     Indicates whether the document has been indexed into the vector store.
    /// </summary>
    public bool IsIndexed { get; set; } = false;

    /// <summary>
    ///     Number of vector chunks created for this document.
    /// </summary>
    public int ChunkCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<SheetChunkEntity> SheetChunks { get; set; } = new();
}