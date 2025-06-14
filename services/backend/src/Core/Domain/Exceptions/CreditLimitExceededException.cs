namespace Domain.Exceptions;

public class CreditLimitExceededException : Exception
{
    public CreditLimitExceededException(string message) : base(message)
    {
    }
}