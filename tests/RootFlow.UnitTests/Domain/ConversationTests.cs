using RootFlow.Domain.Conversations;

namespace RootFlow.UnitTests.Domain;

public sealed class ConversationTests
{
    [Fact]
    public void Constructor_UsesDefaultTitleWhenNoneIsProvided()
    {
        var conversation = new Conversation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal("Conversation", conversation.Title);
    }

    [Fact]
    public void Rename_UsesTrimmedTitle()
    {
        var conversation = new Conversation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Initial",
            DateTime.UtcNow);

        conversation.Rename("  Support Chat  ");

        Assert.Equal("Support Chat", conversation.Title);
    }
}
