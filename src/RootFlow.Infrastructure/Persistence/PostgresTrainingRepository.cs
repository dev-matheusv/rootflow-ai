using System.Text.Json;
using Npgsql;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Domain.Training;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresTrainingRepository : ITrainingRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresTrainingRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Programs
    // ─────────────────────────────────────────────────────────────────────

    public async Task AddProgramAsync(TrainingProgram program, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO training_programs (
                id, workspace_id, name, slug, description, passing_score,
                is_published, created_by_user_id, created_at_utc, updated_at_utc
            )
            VALUES (
                @id, @workspaceId, @name, @slug, @description, @passingScore,
                @isPublished, @createdByUserId, @createdAtUtc, @updatedAtUtc
            );
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        AddProgramParameters(cmd, program);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateProgramAsync(TrainingProgram program, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE training_programs
            SET name = @name,
                description = @description,
                passing_score = @passingScore,
                is_published = @isPublished,
                updated_at_utc = @updatedAtUtc
            WHERE id = @id AND workspace_id = @workspaceId;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        AddProgramParameters(cmd, program);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TrainingProgram?> GetProgramByIdAsync(Guid programId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, workspace_id, name, slug, description, passing_score,
                   is_published, created_by_user_id, created_at_utc, updated_at_utc
            FROM training_programs
            WHERE id = @id AND workspace_id = @workspaceId;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", programId);
        cmd.Parameters.AddWithValue("workspaceId", workspaceId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapProgram(reader) : null;
    }

    public async Task<TrainingProgram?> GetProgramBySlugAsync(string slug, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, workspace_id, name, slug, description, passing_score,
                   is_published, created_by_user_id, created_at_utc, updated_at_utc
            FROM training_programs
            WHERE slug = @slug AND workspace_id = @workspaceId;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("slug", slug);
        cmd.Parameters.AddWithValue("workspaceId", workspaceId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapProgram(reader) : null;
    }

    public async Task<IReadOnlyList<TrainingProgram>> ListProgramsByWorkspaceAsync(
        Guid workspaceId,
        bool publishedOnly,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT id, workspace_id, name, slug, description, passing_score,
                   is_published, created_by_user_id, created_at_utc, updated_at_utc
            FROM training_programs
            WHERE workspace_id = @workspaceId
        """;
        if (publishedOnly) sql += " AND is_published = TRUE";
        sql += " ORDER BY created_at_utc DESC;";

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("workspaceId", workspaceId);

        var results = new List<TrainingProgram>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapProgram(reader));
        }
        return results;
    }

    public async Task<bool> ProgramSlugExistsAsync(string slug, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT 1 FROM training_programs WHERE workspace_id = @workspaceId AND slug = @slug LIMIT 1;";
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("workspaceId", workspaceId);
        cmd.Parameters.AddWithValue("slug", slug);
        return await cmd.ExecuteScalarAsync(cancellationToken) is not null;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Modules
    // ─────────────────────────────────────────────────────────────────────

    public async Task AddModuleAsync(TrainingModule module, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO training_modules (
                id, program_id, order_index, title, description,
                source_document_ids, created_at_utc, updated_at_utc
            )
            VALUES (
                @id, @programId, @orderIndex, @title, @description,
                @sourceDocumentIds, @createdAtUtc, @updatedAtUtc
            );
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        AddModuleParameters(cmd, module);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateModuleAsync(TrainingModule module, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE training_modules
            SET order_index = @orderIndex,
                title = @title,
                description = @description,
                source_document_ids = @sourceDocumentIds,
                updated_at_utc = @updatedAtUtc
            WHERE id = @id;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        AddModuleParameters(cmd, module);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteModuleAsync(Guid moduleId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM training_modules WHERE id = @id;";
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", moduleId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TrainingModule?> GetModuleByIdAsync(Guid moduleId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, program_id, order_index, title, description, source_document_ids,
                   created_at_utc, updated_at_utc
            FROM training_modules
            WHERE id = @id;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", moduleId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapModule(reader) : null;
    }

    public async Task<IReadOnlyList<TrainingModule>> ListModulesByProgramAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, program_id, order_index, title, description, source_document_ids,
                   created_at_utc, updated_at_utc
            FROM training_modules
            WHERE program_id = @programId
            ORDER BY order_index ASC, created_at_utc ASC;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("programId", programId);

        var results = new List<TrainingModule>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapModule(reader));
        }
        return results;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Questions
    // ─────────────────────────────────────────────────────────────────────

    public async Task AddQuestionAsync(TrainingQuestion question, CancellationToken cancellationToken = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await InsertQuestionAsync(conn, transaction: null, question, cancellationToken);
    }

    public async Task AddQuestionsBulkAsync(IEnumerable<TrainingQuestion> questions, CancellationToken cancellationToken = default)
    {
        var list = questions as IList<TrainingQuestion> ?? questions.ToList();
        if (list.Count == 0) return;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await conn.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var q in list)
            {
                await InsertQuestionAsync(conn, transaction, q, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task UpdateQuestionAsync(TrainingQuestion question, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE training_questions
            SET order_index = @orderIndex,
                prompt = @prompt,
                type = @type,
                options = @options::jsonb,
                correct_answer_indices = @correctAnswerIndices,
                explanation = @explanation,
                status = @status,
                updated_at_utc = @updatedAtUtc
            WHERE id = @id;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        AddQuestionUpdateParameters(cmd, question);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteQuestionAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM training_questions WHERE id = @id;";
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", questionId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TrainingQuestion?> GetQuestionByIdAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, module_id, order_index, prompt, type, options, correct_answer_indices,
                   explanation, source_document_id, source_chunk_id, status,
                   created_at_utc, updated_at_utc
            FROM training_questions
            WHERE id = @id;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", questionId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapQuestion(reader) : null;
    }

    public async Task<IReadOnlyList<TrainingQuestion>> ListQuestionsByModuleAsync(
        Guid moduleId,
        bool publishedOnly,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT id, module_id, order_index, prompt, type, options, correct_answer_indices,
                   explanation, source_document_id, source_chunk_id, status,
                   created_at_utc, updated_at_utc
            FROM training_questions
            WHERE module_id = @moduleId
        """;
        if (publishedOnly) sql += " AND status = 'Published'";
        sql += " ORDER BY order_index ASC, created_at_utc ASC;";

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("moduleId", moduleId);

        var results = new List<TrainingQuestion>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapQuestion(reader));
        }
        return results;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Attempts
    // ─────────────────────────────────────────────────────────────────────

    public async Task AddAttemptAsync(TrainingAttempt attempt, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO training_attempts (
                id, module_id, user_id, workspace_id, started_at_utc, completed_at_utc, score, status
            )
            VALUES (
                @id, @moduleId, @userId, @workspaceId, @startedAtUtc, @completedAtUtc, @score, @status
            );
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        AddAttemptParameters(cmd, attempt);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAttemptAsync(TrainingAttempt attempt, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE training_attempts
            SET completed_at_utc = @completedAtUtc,
                score = @score,
                status = @status
            WHERE id = @id;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        AddAttemptParameters(cmd, attempt);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TrainingAttempt?> GetAttemptByIdAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, module_id, user_id, workspace_id, started_at_utc, completed_at_utc, score, status
            FROM training_attempts
            WHERE id = @id;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", attemptId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapAttempt(reader) : null;
    }

    public async Task<IReadOnlyList<TrainingAttempt>> ListAttemptsByUserAndModuleAsync(
        Guid userId,
        Guid moduleId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, module_id, user_id, workspace_id, started_at_utc, completed_at_utc, score, status
            FROM training_attempts
            WHERE user_id = @userId AND module_id = @moduleId
            ORDER BY started_at_utc DESC;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("moduleId", moduleId);

        var results = new List<TrainingAttempt>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapAttempt(reader));
        }
        return results;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Answers
    // ─────────────────────────────────────────────────────────────────────

    public async Task AddAnswersAsync(IEnumerable<TrainingAnswer> answers, CancellationToken cancellationToken = default)
    {
        var list = answers as IList<TrainingAnswer> ?? answers.ToList();
        if (list.Count == 0) return;

        const string sql = """
            INSERT INTO training_answers (id, attempt_id, question_id, selected_indices, is_correct)
            VALUES (@id, @attemptId, @questionId, @selectedIndices, @isCorrect)
            ON CONFLICT (attempt_id, question_id) DO UPDATE
            SET selected_indices = EXCLUDED.selected_indices,
                is_correct = EXCLUDED.is_correct;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await conn.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var answer in list)
            {
                await using var cmd = new NpgsqlCommand(sql, conn, transaction);
                cmd.Parameters.AddWithValue("id", answer.Id);
                cmd.Parameters.AddWithValue("attemptId", answer.AttemptId);
                cmd.Parameters.AddWithValue("questionId", answer.QuestionId);
                cmd.Parameters.AddWithValue("selectedIndices", answer.SelectedIndices.ToArray());
                cmd.Parameters.AddWithValue("isCorrect", answer.IsCorrect);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<TrainingAnswer>> ListAnswersByAttemptAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, attempt_id, question_id, selected_indices, is_correct
            FROM training_answers
            WHERE attempt_id = @attemptId;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("attemptId", attemptId);

        var results = new List<TrainingAnswer>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(TrainingAnswer.Rehydrate(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                ((int[])reader.GetValue(3)).ToList(),
                reader.GetBoolean(4)));
        }
        return results;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Certificates
    // ─────────────────────────────────────────────────────────────────────

    public async Task AddCertificateAsync(TrainingCertificate certificate, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO training_certificates (
                id, program_id, user_id, workspace_id, issued_at_utc, code, pdf_storage_key
            )
            VALUES (
                @id, @programId, @userId, @workspaceId, @issuedAtUtc, @code, @pdfStorageKey
            );
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", certificate.Id);
        cmd.Parameters.AddWithValue("programId", certificate.ProgramId);
        cmd.Parameters.AddWithValue("userId", certificate.UserId);
        cmd.Parameters.AddWithValue("workspaceId", certificate.WorkspaceId);
        cmd.Parameters.AddWithValue("issuedAtUtc", certificate.IssuedAtUtc);
        cmd.Parameters.AddWithValue("code", certificate.Code);
        cmd.Parameters.AddWithValue("pdfStorageKey", certificate.PdfStorageKey);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TrainingCertificate?> GetCertificateByIdAsync(Guid certificateId, Guid workspaceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, program_id, user_id, workspace_id, issued_at_utc, code, pdf_storage_key
            FROM training_certificates
            WHERE id = @id AND workspace_id = @workspaceId;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", certificateId);
        cmd.Parameters.AddWithValue("workspaceId", workspaceId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapCertificate(reader) : null;
    }

    public async Task<TrainingCertificate?> GetCertificateByProgramAndUserAsync(
        Guid programId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, program_id, user_id, workspace_id, issued_at_utc, code, pdf_storage_key
            FROM training_certificates
            WHERE program_id = @programId AND user_id = @userId;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("programId", programId);
        cmd.Parameters.AddWithValue("userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapCertificate(reader) : null;
    }

    public async Task<TrainingCertificate?> GetCertificateByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, program_id, user_id, workspace_id, issued_at_utc, code, pdf_storage_key
            FROM training_certificates
            WHERE code = @code;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("code", code.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapCertificate(reader) : null;
    }

    public async Task<IReadOnlyList<TrainingCertificate>> ListCertificatesByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, program_id, user_id, workspace_id, issued_at_utc, code, pdf_storage_key
            FROM training_certificates
            WHERE user_id = @userId
            ORDER BY issued_at_utc DESC;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("userId", userId);

        var results = new List<TrainingCertificate>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapCertificate(reader));
        }
        return results;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static async Task InsertQuestionAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction? transaction,
        TrainingQuestion question,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO training_questions (
                id, module_id, order_index, prompt, type, options, correct_answer_indices,
                explanation, source_document_id, source_chunk_id, status,
                created_at_utc, updated_at_utc
            )
            VALUES (
                @id, @moduleId, @orderIndex, @prompt, @type, @options::jsonb, @correctAnswerIndices,
                @explanation, @sourceDocumentId, @sourceChunkId, @status,
                @createdAtUtc, @updatedAtUtc
            );
            """;

        await using var cmd = transaction is null
            ? new NpgsqlCommand(sql, conn)
            : new NpgsqlCommand(sql, conn, transaction);

        var optionsJson = JsonSerializer.Serialize(question.Options);
        cmd.Parameters.AddWithValue("id", question.Id);
        cmd.Parameters.AddWithValue("moduleId", question.ModuleId);
        cmd.Parameters.AddWithValue("orderIndex", question.OrderIndex);
        cmd.Parameters.AddWithValue("prompt", question.Prompt);
        cmd.Parameters.AddWithValue("type", question.Type.ToString());
        cmd.Parameters.AddWithValue("options", optionsJson);
        cmd.Parameters.AddWithValue("correctAnswerIndices", question.CorrectAnswerIndices.ToArray());
        cmd.Parameters.AddWithValue("explanation", (object?)question.Explanation ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sourceDocumentId", (object?)question.SourceDocumentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sourceChunkId", (object?)question.SourceChunkId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", question.Status.ToString());
        cmd.Parameters.AddWithValue("createdAtUtc", question.CreatedAtUtc);
        cmd.Parameters.AddWithValue("updatedAtUtc", question.UpdatedAtUtc);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddProgramParameters(NpgsqlCommand cmd, TrainingProgram program)
    {
        cmd.Parameters.AddWithValue("id", program.Id);
        cmd.Parameters.AddWithValue("workspaceId", program.WorkspaceId);
        cmd.Parameters.AddWithValue("name", program.Name);
        cmd.Parameters.AddWithValue("slug", program.Slug);
        cmd.Parameters.AddWithValue("description", (object?)program.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("passingScore", program.PassingScore);
        cmd.Parameters.AddWithValue("isPublished", program.IsPublished);
        cmd.Parameters.AddWithValue("createdByUserId", program.CreatedByUserId);
        cmd.Parameters.AddWithValue("createdAtUtc", program.CreatedAtUtc);
        cmd.Parameters.AddWithValue("updatedAtUtc", program.UpdatedAtUtc);
    }

    private static void AddModuleParameters(NpgsqlCommand cmd, TrainingModule module)
    {
        cmd.Parameters.AddWithValue("id", module.Id);
        cmd.Parameters.AddWithValue("programId", module.ProgramId);
        cmd.Parameters.AddWithValue("orderIndex", module.OrderIndex);
        cmd.Parameters.AddWithValue("title", module.Title);
        cmd.Parameters.AddWithValue("description", (object?)module.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sourceDocumentIds", module.SourceDocumentIds.ToArray());
        cmd.Parameters.AddWithValue("createdAtUtc", module.CreatedAtUtc);
        cmd.Parameters.AddWithValue("updatedAtUtc", module.UpdatedAtUtc);
    }

    private static void AddQuestionUpdateParameters(NpgsqlCommand cmd, TrainingQuestion question)
    {
        var optionsJson = JsonSerializer.Serialize(question.Options);
        cmd.Parameters.AddWithValue("id", question.Id);
        cmd.Parameters.AddWithValue("orderIndex", question.OrderIndex);
        cmd.Parameters.AddWithValue("prompt", question.Prompt);
        cmd.Parameters.AddWithValue("type", question.Type.ToString());
        cmd.Parameters.AddWithValue("options", optionsJson);
        cmd.Parameters.AddWithValue("correctAnswerIndices", question.CorrectAnswerIndices.ToArray());
        cmd.Parameters.AddWithValue("explanation", (object?)question.Explanation ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", question.Status.ToString());
        cmd.Parameters.AddWithValue("updatedAtUtc", question.UpdatedAtUtc);
    }

    private static void AddAttemptParameters(NpgsqlCommand cmd, TrainingAttempt attempt)
    {
        cmd.Parameters.AddWithValue("id", attempt.Id);
        cmd.Parameters.AddWithValue("moduleId", attempt.ModuleId);
        cmd.Parameters.AddWithValue("userId", attempt.UserId);
        cmd.Parameters.AddWithValue("workspaceId", attempt.WorkspaceId);
        cmd.Parameters.AddWithValue("startedAtUtc", attempt.StartedAtUtc);
        cmd.Parameters.AddWithValue("completedAtUtc", (object?)attempt.CompletedAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("score", (object?)attempt.Score ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", attempt.Status.ToString());
    }

    private static TrainingProgram MapProgram(NpgsqlDataReader reader)
    {
        return TrainingProgram.Rehydrate(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetInt32(5),
            reader.GetBoolean(6),
            reader.GetGuid(7),
            DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc),
            DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc));
    }

    private static TrainingModule MapModule(NpgsqlDataReader reader)
    {
        var sourceDocs = (Guid[])reader.GetValue(5);
        return TrainingModule.Rehydrate(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            sourceDocs,
            DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc),
            DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc));
    }

    private static TrainingQuestion MapQuestion(NpgsqlDataReader reader)
    {
        var optionsJson = reader.GetString(5);
        var options = JsonSerializer.Deserialize<string[]>(optionsJson) ?? [];
        var correctIndices = (int[])reader.GetValue(6);
        var type = Enum.Parse<TrainingQuestionType>(reader.GetString(4));
        var status = Enum.Parse<TrainingQuestionStatus>(reader.GetString(10));

        return TrainingQuestion.Rehydrate(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetInt32(2),
            reader.GetString(3),
            type,
            options,
            correctIndices,
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetGuid(8),
            reader.IsDBNull(9) ? null : reader.GetGuid(9),
            status,
            DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc),
            DateTime.SpecifyKind(reader.GetDateTime(12), DateTimeKind.Utc));
    }

    private static TrainingAttempt MapAttempt(NpgsqlDataReader reader)
    {
        return TrainingAttempt.Rehydrate(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetGuid(3),
            DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc),
            reader.IsDBNull(5) ? null : DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc),
            reader.IsDBNull(6) ? null : reader.GetInt32(6),
            Enum.Parse<TrainingAttemptStatus>(reader.GetString(7)));
    }

    private static TrainingCertificate MapCertificate(NpgsqlDataReader reader)
    {
        // SELECT order: id, program_id, user_id, workspace_id, issued_at_utc, code, pdf_storage_key
        // Rehydrate order: id, programId, userId, workspaceId, code, pdfStorageKey, issuedAtUtc
        return TrainingCertificate.Rehydrate(
            id: reader.GetGuid(0),
            programId: reader.GetGuid(1),
            userId: reader.GetGuid(2),
            workspaceId: reader.GetGuid(3),
            code: reader.GetString(5),
            pdfStorageKey: reader.GetString(6),
            issuedAtUtc: DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc));
    }
}
