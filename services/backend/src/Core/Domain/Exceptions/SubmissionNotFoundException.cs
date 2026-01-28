namespace Domain.Exceptions;

public class SubmissionNotFoundException : EntityNotFoundException
{
    public SubmissionNotFoundException(Guid submissionId)
        : base("PluginSubmission", submissionId.ToString()) { }

    public SubmissionNotFoundException(string message)
        : base(message) { }
}
