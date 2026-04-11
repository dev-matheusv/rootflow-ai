using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RootFlow.Application.Abstractions.AI;
using RootFlow.Application.Abstractions.Documents;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.DocumentTemplates.Commands;
using RootFlow.Application.DocumentTemplates.Dtos;
using RootFlow.Application.DocumentTemplates.Queries;
using RootFlow.Domain.Conversations;
using RootFlow.Domain.DocumentTemplates;

namespace RootFlow.Application.DocumentTemplates;

public sealed partial class DocumentTemplateService
{
    private readonly IDocumentTemplateRepository _repository;
    private readonly IDocumentRenderer _renderer;
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly IChatCompletionService _chatCompletion;
    private readonly IClock _clock;

    public DocumentTemplateService(
        IDocumentTemplateRepository repository,
        IDocumentRenderer renderer,
        IDocumentTextExtractor textExtractor,
        IChatCompletionService chatCompletion,
        IClock clock)
    {
        _repository = repository;
        _renderer = renderer;
        _textExtractor = textExtractor;
        _chatCompletion = chatCompletion;
        _clock = clock;
    }

    public async Task<DocumentTemplateSummaryDto> CreateAsync(
        CreateDocumentTemplateCommand command,
        CancellationToken cancellationToken = default)
    {
        var slug = string.IsNullOrWhiteSpace(command.Slug)
            ? await GenerateUniqueSlugAsync(command.Name, command.WorkspaceId, cancellationToken)
            : command.Slug.Trim().ToLowerInvariant();

        var slugExists = await _repository.SlugExistsAsync(slug, command.WorkspaceId, cancellationToken);
        if (slugExists)
        {
            throw new InvalidOperationException($"Já existe um template com o slug '{slug}' neste workspace.");
        }

        var template = new DocumentTemplate(
            Guid.NewGuid(),
            command.WorkspaceId,
            command.Name,
            slug,
            command.Description,
            command.Body,
            _clock.UtcNow);

        foreach (var f in command.Fields)
        {
            var fieldType = ParseFieldType(f.Type);
            template.AddField(new TemplateField(f.Key, f.Label, fieldType, f.IsRequired));
        }

        await _repository.AddAsync(template, cancellationToken);
        return ToSummaryDto(template);
    }

    public async Task<IReadOnlyList<DocumentTemplateSummaryDto>> ListAsync(
        ListDocumentTemplatesQuery query,
        CancellationToken cancellationToken = default)
    {
        var templates = await _repository.ListByWorkspaceAsync(query.WorkspaceId, cancellationToken);
        return templates.Select(ToSummaryDto).ToList();
    }

    public async Task<DocumentTemplateDetailDto?> GetByIdAsync(
        GetDocumentTemplateByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(query.TemplateId, query.WorkspaceId, cancellationToken);
        return template is null ? null : ToDetailDto(template);
    }

    public async Task<byte[]> GenerateAsync(
        GenerateDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(command.TemplateId, command.WorkspaceId, cancellationToken)
            ?? throw new InvalidOperationException($"Template {command.TemplateId} not found.");

        if (!template.IsActive)
        {
            throw new InvalidOperationException($"Template '{template.Name}' is not active.");
        }

        var missingRequired = template.Fields
            .Where(f => f.IsRequired && !command.FieldValues.ContainsKey(f.Key))
            .Select(f => f.Key)
            .ToList();

        if (missingRequired.Count > 0)
        {
            throw new ArgumentException($"Missing required fields: {string.Join(", ", missingRequired)}");
        }

        var renderRequest = new DocumentRenderRequest(
            template.Name,
            template.Body,
            command.FieldValues);

        return await _renderer.RenderAsync(renderRequest, cancellationToken);
    }

    public async Task<DocumentTemplateDraftDto> SuggestFromDescriptionAsync(
        SuggestTemplateFromDescriptionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Description);

        const string jsonSchema =
            """
            {
              "name": "Nome do template",
              "body": "Corpo do documento com {{chave_campo}} para cada variável",
              "fields": [
                {"key": "chave_snake_case", "label": "Rótulo em português", "type": "Text|Date|Number", "isRequired": true}
              ]
            }
            """;

        var prompt = "Crie um template de documento profissional em português brasileiro.\n\n" +
                     "SOLICITAÇÃO DO USUÁRIO:\n" + command.Description + "\n\n" +
                     "REGRAS OBRIGATÓRIAS:\n" +
                     "1. Se o usuário listou campos específicos, use EXATAMENTE esses campos — não invente outros nem omita nenhum.\n" +
                     "2. Cada campo mencionado pelo usuário deve virar um placeholder {{chave_snake_case}} no corpo E um item em \"fields\".\n" +
                     "3. O corpo deve ser um documento real e completo (cabeçalho, texto, assinatura), usando os placeholders nos locais corretos.\n" +
                     "4. Use type=\"Date\" para datas, type=\"Number\" para números, type=\"Text\" para o resto.\n\n" +
                     "Retorne APENAS JSON válido, sem markdown, sem explicação:\n" + jsonSchema;

        return await CallAiForDraftAsync(prompt, cancellationToken);
    }

    public async Task<DocumentTemplateDraftDto> SuggestFromFileAsync(
        SuggestTemplateFromFileCommand command,
        CancellationToken cancellationToken = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"rf_tpl_{Guid.NewGuid()}{Path.GetExtension(command.FileName)}");
        try
        {
            await using (var fs = File.Create(tempPath))
            {
                await command.Content.CopyToAsync(fs, cancellationToken);
            }

            var storedFile = new StoredFile(tempPath, command.FileName, command.ContentType, new FileInfo(tempPath).Length);
            var extractedText = await _textExtractor.ExtractTextAsync(storedFile, cancellationToken);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                var ext = Path.GetExtension(command.FileName).ToUpperInvariant();
                throw new InvalidOperationException(
                    ext == ".PDF"
                        ? "Não foi possível extrair texto do PDF. Se for um documento escaneado (imagem), converta para DOCX ou TXT antes de importar."
                        : "Não foi possível extrair texto do arquivo enviado. Tente converter para DOCX ou TXT.");
            }

            var truncated = extractedText.Length > 6000 ? extractedText[..6000] : extractedText;

            const string fileJsonSchema =
                """
                {
                  "name": "Nome sugerido para o template",
                  "body": "Texto do documento com {{placeholders}} inseridos",
                  "fields": [
                    {"key": "chave_snake_case", "label": "Rótulo em português", "type": "Text|Date|Number", "isRequired": true}
                  ]
                }
                """;

            var prompt = "Analise este template de documento. Identifique todos os campos variáveis " +
                         "(nomes, datas, números, IDs que mudam a cada uso). " +
                         "Substitua as variáveis por {{chave_snake_case}} e liste os campos identificados.\n\n" +
                         "Retorne APENAS JSON válido, sem markdown, sem explicação:\n" + fileJsonSchema +
                         "\n\nDocumento:\n" + truncated;

            return await CallAiForDraftAsync(prompt, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private async Task<DocumentTemplateDraftDto> CallAiForDraftAsync(string prompt, CancellationToken cancellationToken)
    {
        var messages = new List<ChatPromptMessage>
        {
            new(MessageRole.System,
                "Você é um assistente especializado em geração de templates de documentos corporativos brasileiros. " +
                "Responda APENAS com JSON válido e bem formatado, sem markdown, sem blocos de código, sem explicações."),
            new(MessageRole.User, prompt),
        };

        var response = await _chatCompletion.CompleteAsync(new ChatCompletionRequest(messages), cancellationToken);
        var raw = response.Content ?? string.Empty;

        var json = CleanJson(raw);

        AiTemplateDraft? draft = null;
        try
        {
            draft = JsonSerializer.Deserialize<AiTemplateDraft>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException ex)
        {
            var preview = raw.Length > 200 ? raw[..200] : raw;
            throw new InvalidOperationException(
                $"Não foi possível interpretar a resposta da IA. Tente novamente. (Detalhe: {ex.Message} | Resposta: {preview})");
        }

        if (draft is null)
        {
            throw new InvalidOperationException("A IA retornou uma resposta vazia. Tente novamente.");
        }

        var fields = (draft.Fields ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Key))
            .Select(f => new TemplateFieldDto(
                f.Key!,
                string.IsNullOrWhiteSpace(f.Label) ? f.Key! : f.Label,
                f.Type ?? "Text",
                f.IsRequired))
            .ToList();

        return new DocumentTemplateDraftDto(
            string.IsNullOrWhiteSpace(draft.Name) ? "Novo Template" : draft.Name,
            draft.Body ?? "",
            fields);
    }

    private async Task<string> GenerateUniqueSlugAsync(string name, Guid workspaceId, CancellationToken cancellationToken)
    {
        var baseSlug = SlugCleanRegex().Replace(name.ToLowerInvariant(), "-").Trim('-');
        if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = "template";

        var slug = baseSlug;
        var counter = 1;
        while (await _repository.SlugExistsAsync(slug, workspaceId, cancellationToken))
        {
            slug = $"{baseSlug}-{counter++}";
        }
        return slug;
    }

    private static string CleanJson(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : raw;
    }

    private static TemplateFieldType ParseFieldType(string type) => type.ToUpperInvariant() switch
    {
        "DATE" => TemplateFieldType.Date,
        "NUMBER" => TemplateFieldType.Number,
        _ => TemplateFieldType.Text,
    };

    private static TemplateFieldDto ToFieldDto(TemplateField f) =>
        new(f.Key, f.Label, f.Type.ToString(), f.IsRequired);

    private static DocumentTemplateSummaryDto ToSummaryDto(DocumentTemplate t) =>
        new(t.Id, t.WorkspaceId, t.Name, t.Slug, t.Description, t.IsActive,
            t.Fields.Select(ToFieldDto).ToList(),
            t.CreatedAtUtc, t.UpdatedAtUtc);

    private static DocumentTemplateDetailDto ToDetailDto(DocumentTemplate t) =>
        new(t.Id, t.WorkspaceId, t.Name, t.Slug, t.Description, t.Body, t.IsActive,
            t.Fields.Select(ToFieldDto).ToList(),
            t.CreatedAtUtc, t.UpdatedAtUtc);

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugCleanRegex();

    // AI response shape
    private sealed class AiTemplateDraft
    {
        public string? Name { get; set; }
        public string? Body { get; set; }
        public List<AiFieldDraft>? Fields { get; set; }
    }

    private sealed class AiFieldDraft
    {
        public string? Key { get; set; }
        public string? Label { get; set; }
        public string? Type { get; set; }
        public bool IsRequired { get; set; }
    }
}
