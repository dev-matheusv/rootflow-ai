namespace RootFlow.Api.Contracts.DocumentTemplates;

public sealed record GenerateDocumentRequest(
    Dictionary<string, string> FieldValues);
