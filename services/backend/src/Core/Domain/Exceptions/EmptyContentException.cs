namespace Domain.Exceptions;

public class EmptyContentException : Exception
{
    public EmptyContentException()
        : base("Extracted content is empty or null.")
    {
    }

    public EmptyContentException(string message)
        : base(message)
    {
    }
}
