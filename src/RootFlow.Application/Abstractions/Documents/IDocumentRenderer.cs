namespace RootFlow.Application.Abstractions.Documents;

public sealed record DocumentRenderRequest(
    string TemplateName,
    string TemplateBody,
    IReadOnlyDictionary<string, string> FieldValues);

public interface IDocumentRenderer
{
    Task<byte[]> RenderAsync(DocumentRenderRequest request, CancellationToken cancellationToken = default);
}
