namespace Domain.Exceptions;

public class InsufficientCreditsException : Exception
{
    public InsufficientCreditsException(int available, int requested)
        : base($"Insufficient credits: available {available}, requested {requested}.")
    {
        Available = available;
        Requested = requested;
    }

    public int Available { get; }
    public int Requested { get; }
}
