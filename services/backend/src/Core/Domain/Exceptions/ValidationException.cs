namespace Domain.Exceptions;

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }

    public ValidationException(string message, string parameterName)
        : base(message)
    {
        ParameterName = parameterName;
    }

    public string? ParameterName { get; }
}
