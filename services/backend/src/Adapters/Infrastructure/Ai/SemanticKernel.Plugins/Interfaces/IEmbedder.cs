namespace Infrastructure.Ai.SemanticKernel.Plugins.Interfaces;

public interface IEmbedder
{
    ValueTask<float[]> EmbedAsync(string text, CancellationToken ct = default);
}