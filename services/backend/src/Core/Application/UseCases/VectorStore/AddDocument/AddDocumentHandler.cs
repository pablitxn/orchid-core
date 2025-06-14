using Application.Interfaces;
using MediatR;

namespace Application.UseCases.VectorStore.AddDocument;

/// <summary>
///     Handles <see cref="AddDocumentCommand" /> by extracting text, embedding it and storing chunks.
/// </summary>
public class AddDocumentHandler(
    IFileStorageService fileStorage,
    IDocumentTextExtractor textExtractor,
    IEmbeddingGeneratorPort embedder,
    IVectorStorePort vectorStore,
    IDocumentRepository documentRepository)
    : IRequestHandler<AddDocumentCommand, AddDocumentResult>
{
    private readonly IDocumentRepository _documents = documentRepository;
    private readonly IEmbeddingGeneratorPort _embedder = embedder;
    private readonly IDocumentTextExtractor _extractor = textExtractor;
    private readonly IFileStorageService _storage = fileStorage;
    private readonly IVectorStorePort _vectorStore = vectorStore;

    public async Task<AddDocumentResult> Handle(AddDocumentCommand request, CancellationToken cancellationToken)
    {
        await using var stream = await _storage.GetFileAsync(request.DocumentPath, cancellationToken);
        var extension = Path.GetExtension(request.DocumentPath) ?? string.Empty;
        var content = await _extractor.ExtractAsync(stream, extension, cancellationToken);

        if (string.IsNullOrEmpty(content))
        {
            throw new InvalidOperationException("Extracted content is empty or null");
        }

        var docEmbedding = await _embedder.EmbedAsync(content, cancellationToken);
        var paragraphs = content
            .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        Guid fileId;
        var namePart = Path.GetFileNameWithoutExtension(request.DocumentPath);
        var parts = namePart?.Split('_', 2) ?? Array.Empty<string>();
        fileId = parts.Length > 0 && Guid.TryParse(parts[0], out var parsed) ? parsed : Guid.NewGuid();

        for (var i = 0; i < paragraphs.Count; i++)
        {
            var text = paragraphs[i];
            var embedding = await _embedder.EmbedAsync(text, cancellationToken);
            var chunk = new VectorChunk(Guid.NewGuid(), fileId, request.DocumentPath, i, i, text, embedding);
            await _vectorStore.UpsertChunkAsync(chunk, cancellationToken);
        }

        await _documents.UpdateEmbeddingAsync(fileId, docEmbedding, cancellationToken);
        await _documents.UpdateIndexingAsync(fileId, paragraphs.Count, cancellationToken);

        return new AddDocumentResult(request.DocumentPath, paragraphs.Count, request.Metadata);
    }
}