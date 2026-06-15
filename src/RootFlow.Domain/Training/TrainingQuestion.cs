namespace RootFlow.Domain.Training;

public sealed class TrainingQuestion
{
    private readonly List<string> _options = [];
    private readonly List<int> _correctAnswerIndices = [];

    private TrainingQuestion()
    {
    }

    public TrainingQuestion(
        Guid id,
        Guid moduleId,
        int orderIndex,
        string prompt,
        TrainingQuestionType type,
        IEnumerable<string> options,
        IEnumerable<int> correctAnswerIndices,
        string? explanation,
        Guid? sourceDocumentId,
        Guid? sourceChunkId,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Question id cannot be empty.", nameof(id));
        if (moduleId == Guid.Empty) throw new ArgumentException("Module id cannot be empty.", nameof(moduleId));
        if (orderIndex < 0) throw new ArgumentOutOfRangeException(nameof(orderIndex), "Order must be non-negative.");
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var optionList = (options ?? []).ToList();
        var correctList = (correctAnswerIndices ?? []).ToList();
        ValidateOptionsAndAnswers(type, optionList, correctList);

        Id = id;
        ModuleId = moduleId;
        OrderIndex = orderIndex;
        Prompt = prompt.Trim();
        Type = type;
        _options.AddRange(optionList);
        _correctAnswerIndices.AddRange(correctList);
        Explanation = string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim();
        SourceDocumentId = sourceDocumentId;
        SourceChunkId = sourceChunkId;
        Status = TrainingQuestionStatus.Draft;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ModuleId { get; private set; }
    public int OrderIndex { get; private set; }
    public string Prompt { get; private set; } = null!;
    public TrainingQuestionType Type { get; private set; }
    public string? Explanation { get; private set; }
    public Guid? SourceDocumentId { get; private set; }
    public Guid? SourceChunkId { get; private set; }
    public TrainingQuestionStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyList<string> Options => _options.AsReadOnly();
    public IReadOnlyList<int> CorrectAnswerIndices => _correctAnswerIndices.AsReadOnly();

    public void UpdateContent(
        string prompt,
        TrainingQuestionType type,
        IEnumerable<string> options,
        IEnumerable<int> correctAnswerIndices,
        string? explanation,
        DateTime updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var optionList = (options ?? []).ToList();
        var correctList = (correctAnswerIndices ?? []).ToList();
        ValidateOptionsAndAnswers(type, optionList, correctList);

        Prompt = prompt.Trim();
        Type = type;
        _options.Clear();
        _options.AddRange(optionList);
        _correctAnswerIndices.Clear();
        _correctAnswerIndices.AddRange(correctList);
        Explanation = string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim();
        // Edits move a published question back to draft so creator must re-publish.
        Status = TrainingQuestionStatus.Draft;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Publish(DateTime updatedAtUtc)
    {
        Status = TrainingQuestionStatus.Published;
        UpdatedAtUtc = updatedAtUtc;
    }

    public bool IsAnswerCorrect(IEnumerable<int> selectedIndices)
    {
        var selected = (selectedIndices ?? []).Distinct().OrderBy(i => i).ToArray();
        var correct = _correctAnswerIndices.OrderBy(i => i).ToArray();
        return selected.SequenceEqual(correct);
    }

    private static void ValidateOptionsAndAnswers(
        TrainingQuestionType type,
        IReadOnlyList<string> options,
        IReadOnlyList<int> correctIndices)
    {
        if (options.Count < 2)
        {
            throw new ArgumentException("A question must have at least two options.", nameof(options));
        }

        if (options.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Option text cannot be empty.", nameof(options));
        }

        if (correctIndices.Count == 0)
        {
            throw new ArgumentException("At least one correct answer must be marked.", nameof(correctIndices));
        }

        if (correctIndices.Any(i => i < 0 || i >= options.Count))
        {
            throw new ArgumentException("Correct answer indices must point to existing options.", nameof(correctIndices));
        }

        switch (type)
        {
            case TrainingQuestionType.SingleChoice or TrainingQuestionType.TrueFalse:
                if (correctIndices.Count != 1)
                {
                    throw new ArgumentException("Single-choice and true/false questions must have exactly one correct answer.", nameof(correctIndices));
                }
                break;
            case TrainingQuestionType.MultiChoice:
                if (correctIndices.Count < 2)
                {
                    throw new ArgumentException("Multi-choice questions must have at least two correct answers.", nameof(correctIndices));
                }
                break;
        }

        if (type == TrainingQuestionType.TrueFalse && options.Count != 2)
        {
            throw new ArgumentException("True/false questions must have exactly two options.", nameof(options));
        }
    }

    public static TrainingQuestion Rehydrate(
        Guid id,
        Guid moduleId,
        int orderIndex,
        string prompt,
        TrainingQuestionType type,
        IEnumerable<string> options,
        IEnumerable<int> correctAnswerIndices,
        string? explanation,
        Guid? sourceDocumentId,
        Guid? sourceChunkId,
        TrainingQuestionStatus status,
        DateTime createdAtUtc,
        DateTime updatedAtUtc)
    {
        var question = new TrainingQuestion
        {
            Id = id,
            ModuleId = moduleId,
            OrderIndex = orderIndex,
            Prompt = prompt,
            Type = type,
            Explanation = explanation,
            SourceDocumentId = sourceDocumentId,
            SourceChunkId = sourceChunkId,
            Status = status,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc,
        };
        question._options.AddRange(options ?? []);
        question._correctAnswerIndices.AddRange(correctAnswerIndices ?? []);
        return question;
    }
}
