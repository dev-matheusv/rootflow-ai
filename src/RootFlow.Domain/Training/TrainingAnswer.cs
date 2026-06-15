namespace RootFlow.Domain.Training;

public sealed class TrainingAnswer
{
    private readonly List<int> _selectedIndices = [];

    private TrainingAnswer()
    {
    }

    public TrainingAnswer(
        Guid id,
        Guid attemptId,
        Guid questionId,
        IEnumerable<int> selectedIndices,
        bool isCorrect)
    {
        if (id == Guid.Empty) throw new ArgumentException("Answer id cannot be empty.", nameof(id));
        if (attemptId == Guid.Empty) throw new ArgumentException("Attempt id cannot be empty.", nameof(attemptId));
        if (questionId == Guid.Empty) throw new ArgumentException("Question id cannot be empty.", nameof(questionId));

        Id = id;
        AttemptId = attemptId;
        QuestionId = questionId;
        _selectedIndices.AddRange(selectedIndices ?? []);
        IsCorrect = isCorrect;
    }

    public Guid Id { get; private set; }
    public Guid AttemptId { get; private set; }
    public Guid QuestionId { get; private set; }
    public bool IsCorrect { get; private set; }

    public IReadOnlyList<int> SelectedIndices => _selectedIndices.AsReadOnly();

    public static TrainingAnswer Rehydrate(
        Guid id,
        Guid attemptId,
        Guid questionId,
        IEnumerable<int> selectedIndices,
        bool isCorrect)
    {
        var answer = new TrainingAnswer
        {
            Id = id,
            AttemptId = attemptId,
            QuestionId = questionId,
            IsCorrect = isCorrect,
        };
        answer._selectedIndices.AddRange(selectedIndices ?? []);
        return answer;
    }
}
