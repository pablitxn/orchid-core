namespace Domain.Exceptions;

public class OperationFailedException : Exception
{
    public OperationFailedException(string operation)
        : base($"Operation '{operation}' failed after maximum retry attempts.")
    {
        Operation = operation;
    }

    public OperationFailedException(string operation, Exception innerException)
        : base($"Operation '{operation}' failed: {innerException.Message}", innerException)
    {
        Operation = operation;
    }

    public string Operation { get; }
}
