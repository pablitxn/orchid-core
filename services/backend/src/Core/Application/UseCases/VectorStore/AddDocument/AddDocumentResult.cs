namespace Application.UseCases.VectorStore.AddDocument;

/// <summary>
///     Result information after adding a document.
/// </summary>
public sealed record AddDocumentResult(string DocumentPath, int Chunks, string? Metadata);