namespace RootFlow.Application.Training;

public sealed class TrainingOptions
{
    // Base URL used to build the public verification URL printed on certificates.
    // Default points to the production frontend; override via config in dev/staging.
    public string PublicVerificationBaseUrl { get; set; } = "https://www.rootflow.com.br/verify";
}
