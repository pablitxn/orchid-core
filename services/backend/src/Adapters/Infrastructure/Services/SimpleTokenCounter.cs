using Application.Interfaces;

namespace Infrastructure.Services;

/// <summary>
/// Simple token counter that estimates tokens based on word count.
/// Approximation: ~0.75 tokens per word for English text.
/// </summary>
public class SimpleTokenCounter : ITokenCounter
{
    public int CountTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        // Simple approximation: split by whitespace and multiply by 1.3
        // This gives a rough estimate for GPT-style tokenization
        var wordCount = text.Split(
            [' ', '\n', '\r', '\t'],
            StringSplitOptions.RemoveEmptyEntries).Length;

        return (int)(wordCount * 1.3);
    }
}
