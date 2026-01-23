namespace Application.UseCases.VectorStore.AddDocument;

/// <summary>
///     Result information after adding a document.
/// </summary>
public sealed record AddDocumentDto(string DocumentPath, int Chunks, string? Metadata);