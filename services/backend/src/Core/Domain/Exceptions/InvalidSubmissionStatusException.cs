using Domain.Enums;

namespace Domain.Exceptions;

public class InvalidSubmissionStatusException : Exception
{
    public Guid SubmissionId { get; }
    public SubmissionStatus CurrentStatus { get; }
    public SubmissionStatus ExpectedStatus { get; }

    public InvalidSubmissionStatusException(Guid submissionId, SubmissionStatus currentStatus, SubmissionStatus expectedStatus)
        : base($"Submission '{submissionId}' has status '{currentStatus}', expected '{expectedStatus}'.")
    {
        SubmissionId = submissionId;
        CurrentStatus = currentStatus;
        ExpectedStatus = expectedStatus;
    }
}
