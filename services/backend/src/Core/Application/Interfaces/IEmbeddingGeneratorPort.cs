namespace Application.Interfaces;

/// <summary>
///     Port for generating embeddings from text.
/// </summary>
public interface IEmbeddingGeneratorPort
{
    /// <summary>
    ///     Generates an embedding vector for the given input text.
    /// </summary>
    /// <param name="input">The text to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Embedding vector as array of floats.</returns>
    Task<float[]> EmbedAsync(string input, CancellationToken cancellationToken = default);
}