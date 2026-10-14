using ClientPortal.Domain.Exceptions;

namespace ClientPortal.Domain.Entities;

/// <summary>
/// A contact card kept by the workspace. It is archived instead of deleted, so the projects and history that will
/// point to it are never lost. Once the client accepts an invitation the card is linked to their account, and from then
/// on their name, email, phone and company are read from that account (see <c>ClientResponseBuilder</c>); only the
/// notes stay the workspace's own.
/// </summary>
public sealed class Client : Entity
{
    private Client()
    {
    }

    private Client(Guid workspaceId, DateTime createdAtUtc)
    {
        WorkspaceId = workspaceId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid WorkspaceId { get; private set; }

    /// <summary>Optional: a card can start with just an email. Until the client has an account, the UI shows the email instead.</summary>
    public string? Name { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string? Company { get; private set; }

    public string? Phone { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>The client's own account, once they accepted an invitation.</summary>
    public Guid? UserId { get; private set; }

    public DateTime? LinkedAtUtc { get; private set; }

    public bool IsLinked => UserId is not null;

    public DateTime? ArchivedAtUtc { get; private set; }

    public bool IsArchived => ArchivedAtUtc is not null;

    public static Client Register(
        Guid workspaceId,
        string? name,
        string email,
        string? company = null,
        string? phone = null,
        string? notes = null)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new DomainException("A client belongs to a workspace.");
        }

        var client = new Client(workspaceId, DateTime.UtcNow);
        client.UpdateDetails(name, email, company, phone, notes);

        return client;
    }

    public void UpdateDetails(string? name, string email, string? company, string? phone, string? notes)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Client email is required.");
        }

        Name = Optional(name);
        Email = NormalizeEmail(email);
        Company = Optional(company);
        Phone = Optional(phone);
        Notes = Optional(notes);
    }

    /// <summary>Changes only the notes, the one part of a client the workspace keeps even when the rest is locked.</summary>
    public void UpdateNotes(string? notes) => Notes = Optional(notes);

    /// <summary>Connects the card to the client's account. A card can be linked once and only while it is not archived.</summary>
    public void LinkTo(Guid userId, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A client can only be linked to an account.");
        }

        if (IsLinked)
        {
            throw new DomainException("This client is already linked to an account.");
        }

        if (IsArchived)
        {
            throw new DomainException("An archived client cannot be linked to an account.");
        }

        UserId = userId;
        LinkedAtUtc = nowUtc;
    }

    /// <summary>Hides the client from the default list. Archiving twice keeps the first date.</summary>
    public void Archive() => ArchivedAtUtc ??= DateTime.UtcNow;

    public void Restore() => ArchivedAtUtc = null;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    // A blank optional field means "not set", so the API never returns empty strings.
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
