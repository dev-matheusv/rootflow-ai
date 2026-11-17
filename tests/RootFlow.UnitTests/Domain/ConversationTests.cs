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

    // RF-18: conversations are private to whoever started them.
    [Fact]
    public void IsVisibleTo_OwnerOnly_EvenForAdmins()
    {
        var owner = Guid.NewGuid();
        var conversation = new Conversation(Guid.NewGuid(), Guid.NewGuid(), "Case A", DateTime.UtcNow, owner);

        Assert.True(conversation.IsVisibleTo(owner, isWorkspaceAdmin: false));
        Assert.False(conversation.IsVisibleTo(Guid.NewGuid(), isWorkspaceAdmin: false));
        Assert.False(conversation.IsVisibleTo(Guid.NewGuid(), isWorkspaceAdmin: true));
        Assert.False(conversation.IsVisibleTo(Guid.Empty, isWorkspaceAdmin: true));
    }

    [Fact]
    public void IsVisibleTo_UnownedLegacyConversation_OnlyAdmins()
    {
        var conversation = new Conversation(Guid.NewGuid(), Guid.NewGuid(), "Old", DateTime.UtcNow);

        Assert.Null(conversation.CreatedByUserId);
        Assert.True(conversation.IsVisibleTo(Guid.NewGuid(), isWorkspaceAdmin: true));
        Assert.False(conversation.IsVisibleTo(Guid.NewGuid(), isWorkspaceAdmin: false));
    }
}
