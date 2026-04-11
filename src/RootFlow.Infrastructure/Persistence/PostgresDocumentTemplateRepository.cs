using System.Text.Json;
using Npgsql;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Domain.DocumentTemplates;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresDocumentTemplateRepository : IDocumentTemplateRepository
{
    private readonly NpgsqlDataSource _dataSource;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public PostgresDocumentTemplateRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task AddAsync(DocumentTemplate template, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO document_templates (
                id, workspace_id, name, slug, description, body,
                fields, is_active, created_at_utc, updated_at_utc
            )
            VALUES (
                @id, @workspaceId, @name, @slug, @description, @body,
                @fields::jsonb, @isActive, @createdAtUtc, @updatedAtUtc
            );
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        AddParameters(cmd, template);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(DocumentTemplate template, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE document_templates
            SET name = @name,
                slug = @slug,
                description = @description,
                body = @body,
                fields = @fields::jsonb,
                is_active = @isActive,
                updated_at_utc = @updatedAtUtc
            WHERE id = @id AND workspace_id = @workspaceId;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        AddParameters(cmd, template);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DocumentTemplate?> GetByIdAsync(Guid templateId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, workspace_id, name, slug, description, body, fields, is_active, created_at_utc, updated_at_utc
            FROM document_templates
            WHERE id = @id AND workspace_id = @workspaceId;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", templateId);
        cmd.Parameters.AddWithValue("workspaceId", workspaceId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapTemplate(reader) : null;
    }

    public async Task<IReadOnlyList<DocumentTemplate>> ListByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, workspace_id, name, slug, description, body, fields, is_active, created_at_utc, updated_at_utc
            FROM document_templates
            WHERE workspace_id = @workspaceId AND is_active = TRUE
            ORDER BY created_at_utc DESC;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("workspaceId", workspaceId);

        var results = new List<DocumentTemplate>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapTemplate(reader));
        }
        return results;
    }

    public async Task<bool> SlugExistsAsync(string slug, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 1 FROM document_templates
            WHERE workspace_id = @workspaceId AND slug = @slug
            LIMIT 1;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("workspaceId", workspaceId);
        cmd.Parameters.AddWithValue("slug", slug);
        return await cmd.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static DocumentTemplate MapTemplate(NpgsqlDataReader reader)
    {
        var fieldsJson = reader.GetString(6);
        var fields = DeserializeFields(fieldsJson);

        return DocumentTemplate.Rehydrate(
            id: reader.GetGuid(0),
            workspaceId: reader.GetGuid(1),
            name: reader.GetString(2),
            slug: reader.GetString(3),
            description: reader.IsDBNull(4) ? null : reader.GetString(4),
            body: reader.GetString(5),
            isActive: reader.GetBoolean(7),
            fields: fields,
            createdAtUtc: DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc),
            updatedAtUtc: DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc));
    }

    private static IEnumerable<TemplateField> DeserializeFields(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return [];

        var dtos = JsonSerializer.Deserialize<FieldJsonDto[]>(json, JsonOptions);
        if (dtos is null) return [];

        return dtos.Select(d => new TemplateField(
            d.Key,
            d.Label,
            Enum.TryParse<TemplateFieldType>(d.Type, true, out var type) ? type : TemplateFieldType.Text,
            d.IsRequired));
    }

    private static void AddParameters(NpgsqlCommand cmd, DocumentTemplate template)
    {
        var fieldsJson = JsonSerializer.Serialize(
            template.Fields.Select(f => new FieldJsonDto(f.Key, f.Label, f.Type.ToString(), f.IsRequired)),
            JsonOptions);

        cmd.Parameters.AddWithValue("id", template.Id);
        cmd.Parameters.AddWithValue("workspaceId", template.WorkspaceId);
        cmd.Parameters.AddWithValue("name", template.Name);
        cmd.Parameters.AddWithValue("slug", template.Slug);
        cmd.Parameters.AddWithValue("description", (object?)template.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("body", template.Body);
        cmd.Parameters.AddWithValue("fields", fieldsJson);
        cmd.Parameters.AddWithValue("isActive", template.IsActive);
        cmd.Parameters.AddWithValue("createdAtUtc", template.CreatedAtUtc);
        cmd.Parameters.AddWithValue("updatedAtUtc", template.UpdatedAtUtc);
    }

    private sealed record FieldJsonDto(string Key, string Label, string Type, bool IsRequired);
}
