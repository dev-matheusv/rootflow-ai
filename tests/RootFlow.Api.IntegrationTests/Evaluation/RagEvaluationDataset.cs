namespace RootFlow.Api.IntegrationTests.Evaluation;

public static class RagEvaluationDataset
{
    public static IReadOnlyList<EvaluationDocument> Documents { get; } =
    [
        new EvaluationDocument("remote-work-policy.md"),
        new EvaluationDocument("support-runbook.md"),
        new EvaluationDocument("billing-faq.md")
    ];

    public static IReadOnlyList<EvaluationCase> Cases { get; } =
    [
        new(
            "remote-work-days",
            "How many remote days are allowed each week?",
            "remote-work-policy.md",
            "three days per week",
            "[1]"),
        new(
            "password-reset",
            "How do support leads reset an enterprise customer password?",
            "support-runbook.md",
            "open the Admin Portal",
            "[1]"),
        new(
            "invoice-date",
            "When are invoices generated?",
            "billing-faq.md",
            "first business day of each month",
            "[1]"),
        new(
            "unsupported-question",
            "What is the annual retention target?",
            null,
            "I do not know based on the current knowledge base.",
            null,
            true)
    ];

    public static string GetDocumentPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "TestData", "RagEvaluation", fileName);
    }
}

public sealed record EvaluationDocument(string FileName)
{
    public string Path => RagEvaluationDataset.GetDocumentPath(FileName);
}

public sealed record EvaluationCase(
    string Id,
    string Question,
    string? ExpectedTopDocument,
    string ExpectedAnswerFragment,
    string? ExpectedCitationMarker,
    bool ExpectInsufficientContext = false);
