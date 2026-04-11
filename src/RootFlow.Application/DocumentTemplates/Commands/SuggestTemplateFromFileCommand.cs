namespace RootFlow.Application.DocumentTemplates.Commands;

public sealed record SuggestTemplateFromFileCommand(
    string FileName,
    string ContentType,
    Stream Content);
