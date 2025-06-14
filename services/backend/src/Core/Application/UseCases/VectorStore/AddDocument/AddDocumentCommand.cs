using MediatR;

namespace Application.UseCases.VectorStore.AddDocument;

/// <summary>
///     Command to add a document into the vector store.
/// </summary>
public sealed record AddDocumentCommand(string DocumentPath, string? Metadata) : IRequest<AddDocumentResult>;