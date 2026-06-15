using RootFlow.Domain.Training;

namespace RootFlow.UnitTests.Domain;

public sealed class TrainingTests
{
    private static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Program_NewlyCreated_IsUnpublishedWithDefaultPassingScore()
    {
        var program = new TrainingProgram(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Onboarding",
            "onboarding",
            "Trilha inicial",
            Guid.NewGuid(),
            Now);

        Assert.False(program.IsPublished);
        Assert.Equal(70, program.PassingScore);
        Assert.Equal("onboarding", program.Slug);
    }

    [Fact]
    public void Program_UpdateDetails_RejectsScoresOutOfRange()
    {
        var program = NewProgram();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            program.UpdateDetails("New", null, -5, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            program.UpdateDetails("New", null, 150, Now));
    }

    [Theory]
    [InlineData(TrainingQuestionType.SingleChoice, new[] { "A", "B", "C" }, new[] { 0 })]
    [InlineData(TrainingQuestionType.TrueFalse, new[] { "Verdadeiro", "Falso" }, new[] { 1 })]
    [InlineData(TrainingQuestionType.MultiChoice, new[] { "A", "B", "C", "D" }, new[] { 0, 2 })]
    public void Question_AcceptsValidShapes(
        TrainingQuestionType type,
        string[] options,
        int[] correctIndices)
    {
        var q = new TrainingQuestion(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            "Prompt",
            type,
            options,
            correctIndices,
            null,
            null,
            null,
            Now);

        Assert.Equal(TrainingQuestionStatus.Draft, q.Status);
        Assert.Equal(options.Length, q.Options.Count);
    }

    [Fact]
    public void Question_RejectsSingleChoiceWithMultipleCorrectAnswers()
    {
        Assert.Throws<ArgumentException>(() =>
            new TrainingQuestion(
                Guid.NewGuid(),
                Guid.NewGuid(),
                0,
                "Prompt",
                TrainingQuestionType.SingleChoice,
                ["A", "B", "C"],
                [0, 1],
                null,
                null,
                null,
                Now));
    }

    [Fact]
    public void Question_RejectsMultiChoiceWithSingleCorrectAnswer()
    {
        Assert.Throws<ArgumentException>(() =>
            new TrainingQuestion(
                Guid.NewGuid(),
                Guid.NewGuid(),
                0,
                "Prompt",
                TrainingQuestionType.MultiChoice,
                ["A", "B", "C"],
                [1],
                null,
                null,
                null,
                Now));
    }

    [Fact]
    public void Question_RejectsCorrectIndexOutsideOptions()
    {
        Assert.Throws<ArgumentException>(() =>
            new TrainingQuestion(
                Guid.NewGuid(),
                Guid.NewGuid(),
                0,
                "Prompt",
                TrainingQuestionType.SingleChoice,
                ["A", "B"],
                [5],
                null,
                null,
                null,
                Now));
    }

    [Fact]
    public void Question_RejectsTrueFalseWithExtraOptions()
    {
        Assert.Throws<ArgumentException>(() =>
            new TrainingQuestion(
                Guid.NewGuid(),
                Guid.NewGuid(),
                0,
                "Prompt",
                TrainingQuestionType.TrueFalse,
                ["Verdadeiro", "Falso", "Talvez"],
                [0],
                null,
                null,
                null,
                Now));
    }

    [Fact]
    public void Question_IsAnswerCorrect_OrderIndependentForMultiChoice()
    {
        var q = new TrainingQuestion(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            "Prompt",
            TrainingQuestionType.MultiChoice,
            ["A", "B", "C", "D"],
            [0, 2],
            null,
            null,
            null,
            Now);

        Assert.True(q.IsAnswerCorrect([0, 2]));
        Assert.True(q.IsAnswerCorrect([2, 0]));
        Assert.False(q.IsAnswerCorrect([0]));
        Assert.False(q.IsAnswerCorrect([0, 1, 2]));
    }

    [Fact]
    public void Question_UpdateContent_RevertsPublishedToDraft()
    {
        var q = new TrainingQuestion(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            "Prompt",
            TrainingQuestionType.SingleChoice,
            ["A", "B"],
            [0],
            null,
            null,
            null,
            Now);
        q.Publish(Now);
        Assert.Equal(TrainingQuestionStatus.Published, q.Status);

        q.UpdateContent("Novo prompt", TrainingQuestionType.SingleChoice, ["A", "B"], [1], null, Now.AddMinutes(1));

        Assert.Equal(TrainingQuestionStatus.Draft, q.Status);
    }

    [Fact]
    public void Attempt_Complete_MarksPassedWhenScoreAtOrAbovePassing()
    {
        var attempt = new TrainingAttempt(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now);

        attempt.Complete(score: 80, passingScore: 70, completedAtUtc: Now.AddMinutes(5));

        Assert.Equal(TrainingAttemptStatus.Passed, attempt.Status);
        Assert.Equal(80, attempt.Score);
        Assert.NotNull(attempt.CompletedAtUtc);
    }

    [Fact]
    public void Attempt_Complete_MarksFailedBelowPassingScore()
    {
        var attempt = new TrainingAttempt(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now);

        attempt.Complete(score: 60, passingScore: 70, completedAtUtc: Now.AddMinutes(5));

        Assert.Equal(TrainingAttemptStatus.Failed, attempt.Status);
        Assert.Equal(60, attempt.Score);
    }

    [Fact]
    public void Attempt_Complete_RejectsDoubleCompletion()
    {
        var attempt = new TrainingAttempt(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now);
        attempt.Complete(80, 70, Now.AddMinutes(5));

        Assert.Throws<InvalidOperationException>(() =>
            attempt.Complete(90, 70, Now.AddMinutes(10)));
    }

    [Fact]
    public void Certificate_NormalizesCodeToUppercase()
    {
        var cert = new TrainingCertificate(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            code: "abc123xyz789",
            pdfStorageKey: "workspace-1/certificates/abc.pdf",
            issuedAtUtc: Now);

        Assert.Equal("ABC123XYZ789", cert.Code);
    }

    private static TrainingProgram NewProgram() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "P", "p", null, Guid.NewGuid(), Now);
}
