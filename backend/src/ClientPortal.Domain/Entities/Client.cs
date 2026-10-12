using ClientPortal.Domain.Exceptions;

namespace ClientPortal.Domain.Entities;

/// <summary>
/// A contact card kept by the workspace. It is archived instead of deleted, so the projects and history that will
/// point to it are never lost.
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

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? Company { get; private set; }

    public string? Phone { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ArchivedAtUtc { get; private set; }

    public bool IsArchived => ArchivedAtUtc is not null;

    public static Client Register(
        Guid workspaceId,
        string name,
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

    public void UpdateDetails(string name, string email, string? company, string? phone, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Client name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Client email is required.");
        }

        Name = name.Trim();
        Email = NormalizeEmail(email);
        Company = Optional(company);
        Phone = Optional(phone);
        Notes = Optional(notes);
    }

    /// <summary>Hides the client from the default list. Archiving twice keeps the first date.</summary>
    public void Archive() => ArchivedAtUtc ??= DateTime.UtcNow;

    public void Restore() => ArchivedAtUtc = null;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    // A blank optional field means "not set", so the API never returns empty strings.
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
