using Application.Interfaces;
using Application.UseCases.VectorStore.AddDocument;
using Moq;

namespace Application.Tests.UseCases.VectorStore.AddDocument;

public class AddDocumentHandlerTests
{
    [Fact]
    public async Task Handle_AddsChunksAndUpdatesRepository()
    {
        var documentPath = "doc.txt";
        var metadata = "m";
        var fileStream = new MemoryStream();

        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.GetFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(fileStream);

        var extractor = new Mock<IDocumentTextExtractor>();
        extractor.Setup(e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("para1\n\npara2");

        var embedder = new Mock<IEmbeddingGeneratorPort>();
        embedder.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 0f });

        var vector = new Mock<IVectorStorePort>();
        var repo = new Mock<IDocumentRepository>();

        var handler = new AddDocumentHandler(storage.Object, extractor.Object, embedder.Object, vector.Object,
            repo.Object);

        var result = await handler.Handle(new AddDocumentCommand(documentPath, metadata), CancellationToken.None);

        Assert.Equal(documentPath, result.DocumentPath);
        Assert.Equal(2, result.Chunks);
        vector.Verify(v => v.UpsertChunkAsync(It.IsAny<VectorChunk>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        repo.Verify(r => r.UpdateEmbeddingAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
        repo.Verify(r => r.UpdateIndexingAsync(It.IsAny<Guid>(), 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenFileNotFound_ThrowsFileNotFoundException()
    {
        var documentPath = "nonexistent.txt";
        var metadata = "m";
        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.GetFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());

        var extractor = new Mock<IDocumentTextExtractor>();
        var embedder = new Mock<IEmbeddingGeneratorPort>();
        var vector = new Mock<IVectorStorePort>();
        var repo = new Mock<IDocumentRepository>();

        var handler = new AddDocumentHandler(storage.Object, extractor.Object, embedder.Object, vector.Object,
            repo.Object);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            handler.Handle(new AddDocumentCommand(documentPath, metadata), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenInvalidFileFormat_ThrowsFormatException()
    {
        var documentPath = "doc.unsupported";
        var metadata = "m";
        var fileStream = new MemoryStream();

        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.GetFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileStream);

        var extractor = new Mock<IDocumentTextExtractor>();
        extractor.Setup(e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FormatException("Invalid format"));

        var embedder = new Mock<IEmbeddingGeneratorPort>();
        var vector = new Mock<IVectorStorePort>();
        var repo = new Mock<IDocumentRepository>();

        var handler = new AddDocumentHandler(storage.Object, extractor.Object, embedder.Object, vector.Object,
            repo.Object);

        await Assert.ThrowsAsync<FormatException>(() =>
            handler.Handle(new AddDocumentCommand(documentPath, metadata), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenEmbeddingFails_ThrowsInvalidOperationException()
    {
        var documentPath = "doc.txt";
        var metadata = "m";
        var fileStream = new MemoryStream();

        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.GetFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileStream);

        var extractor = new Mock<IDocumentTextExtractor>();
        extractor.Setup(e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("content");

        var embedder = new Mock<IEmbeddingGeneratorPort>();
        embedder.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding error"));

        var vector = new Mock<IVectorStorePort>();
        var repo = new Mock<IDocumentRepository>();

        var handler = new AddDocumentHandler(storage.Object, extractor.Object, embedder.Object, vector.Object,
            repo.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new AddDocumentCommand(documentPath, metadata), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenUpdateEmbeddingFails_ThrowsException()
    {
        var documentPath = "doc.txt";
        var metadata = "m";
        var fileStream = new MemoryStream();

        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.GetFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileStream);

        var extractor = new Mock<IDocumentTextExtractor>();
        extractor.Setup(e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("para1\n\npara2");

        var embedder = new Mock<IEmbeddingGeneratorPort>();
        embedder.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 0f });

        var vector = new Mock<IVectorStorePort>();
        var repo = new Mock<IDocumentRepository>();
        repo.Setup(r => r.UpdateEmbeddingAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Update embedding error"));
        repo.Setup(r => r.UpdateIndexingAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new AddDocumentHandler(storage.Object, extractor.Object, embedder.Object, vector.Object,
            repo.Object);

        await Assert.ThrowsAsync<Exception>(() =>
            handler.Handle(new AddDocumentCommand(documentPath, metadata), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenUpdateIndexingFails_ThrowsException()
    {
        var documentPath = "doc.txt";
        var metadata = "m";
        var fileStream = new MemoryStream();

        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.GetFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileStream);

        var extractor = new Mock<IDocumentTextExtractor>();
        extractor.Setup(e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("para1\n\npara2");

        var embedder = new Mock<IEmbeddingGeneratorPort>();
        embedder.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 0f });

        var vector = new Mock<IVectorStorePort>();
        var repo = new Mock<IDocumentRepository>();
        repo.Setup(r => r.UpdateEmbeddingAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.UpdateIndexingAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Update indexing error"));

        var handler = new AddDocumentHandler(storage.Object, extractor.Object, embedder.Object, vector.Object,
            repo.Object);

        await Assert.ThrowsAsync<Exception>(() =>
            handler.Handle(new AddDocumentCommand(documentPath, metadata), CancellationToken.None));
    }
}