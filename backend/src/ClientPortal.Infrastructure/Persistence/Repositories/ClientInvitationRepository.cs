using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientPortal.Infrastructure.Persistence.Repositories;

internal sealed class ClientInvitationRepository(ApplicationDbContext dbContext) : IClientInvitationRepository
{
    public Task<ClientInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.ClientInvitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

    public Task<ClientInvitation?> GetOpenForClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.ClientInvitations.FirstOrDefaultAsync(
            i => i.ClientId == clientId && i.AcceptedAtUtc == null && i.CancelledAtUtc == null,
            cancellationToken);

    public async Task<IReadOnlyList<ClientInvitation>> ListOpenForClientsAsync(
        IReadOnlyCollection<Guid> clientIds,
        CancellationToken cancellationToken)
    {
        var ids = clientIds.ToArray();

        return await dbContext.ClientInvitations
            .AsNoTracking()
            .Where(i => ids.Contains(i.ClientId) && i.AcceptedAtUtc == null && i.CancelledAtUtc == null)
            .ToListAsync(cancellationToken);
    }

    public void Add(ClientInvitation invitation) => dbContext.ClientInvitations.Add(invitation);
}
