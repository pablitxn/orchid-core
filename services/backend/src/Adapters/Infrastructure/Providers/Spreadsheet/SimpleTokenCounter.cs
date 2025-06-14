using Application.Interfaces;

namespace Infrastructure.Providers;

/// <summary>
///     Naive token counter that approximates tokens from character length.
///     Assumes roughly four characters per token.
/// </summary>
public sealed class SimpleTokenCounter : ITokenCounter
{
    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}