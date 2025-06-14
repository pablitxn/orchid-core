namespace Application.Interfaces;

public interface ITokenCounter
{
    /// <summary>
    ///     Estimates the number of tokens for the given text.
    /// </summary>
    int CountTokens(string text);
}