namespace RootFlow.Application.Training;

public sealed class TrainingNotFoundException : Exception
{
    public TrainingNotFoundException(string message) : base(message)
    {
    }
}

public sealed class TrainingPublishValidationException : Exception
{
    public TrainingPublishValidationException(string message) : base(message)
    {
    }
}

public sealed class WorkspaceTrainingDisabledException : Exception
{
    public WorkspaceTrainingDisabledException()
        : base("Training is not enabled for this workspace.")
    {
    }
}
