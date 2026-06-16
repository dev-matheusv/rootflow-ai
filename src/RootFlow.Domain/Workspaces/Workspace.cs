namespace RootFlow.Domain.Workspaces;

public sealed class Workspace
{
    private Workspace()
    {
    }

    public Workspace(Guid id, string name, string slug, DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Workspace id cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        Id = id;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
        TrainingEnabled = false;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public bool IsActive { get; private set; }

    // Feature flag for the T&D add-on. Off by default. Flipped by a platform
    // admin (Phase E1) or by the Stripe webhook when the customer buys the
    // add-on (Phase E2 — not in this MVP).
    public bool TrainingEnabled { get; private set; }

    public void SetTrainingEnabled(bool enabled)
    {
        TrainingEnabled = enabled;
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
