namespace RootFlow.Application.Abstractions.Documents;

/// <summary>Tokens spent by an AI model while extracting text (for example, reading an image).</summary>
public sealed record DocumentExtractionUsage(
    string Provider,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens);

/// <summary>
/// RF-06: extracted text plus the AI usage it cost, so the caller can charge the workspace credits.
/// Usage is null when no AI model was involved (PDF, DOCX, text...).
/// </summary>
public sealed record DocumentExtraction(string Text, DocumentExtractionUsage? Usage);
