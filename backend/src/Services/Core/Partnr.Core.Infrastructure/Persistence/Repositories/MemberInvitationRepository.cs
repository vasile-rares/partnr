using Partnr.Core.Application.Interfaces.Repositories;
using Partnr.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Partnr.Core.Infrastructure.Persistence.Repositories;

internal sealed class MemberInvitationRepository(ApplicationDbContext dbContext) : IMemberInvitationRepository
{
    public Task<MemberInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.MemberInvitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

    public Task<MemberInvitation?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        dbContext.MemberInvitations.FirstOrDefaultAsync(i => i.WorkspaceId == workspaceId && i.Id == id, cancellationToken);

    public Task<MemberInvitation?> GetOpenForEmailAsync(Guid workspaceId, string email, CancellationToken cancellationToken) =>
        dbContext.MemberInvitations.FirstOrDefaultAsync(
            i => i.WorkspaceId == workspaceId && i.Email == email && i.AcceptedAtUtc == null && i.CancelledAtUtc == null,
            cancellationToken);

    public async Task<IReadOnlyList<MemberInvitation>> ListOpenAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        await dbContext.MemberInvitations
            .AsNoTracking()
            .Where(i => i.WorkspaceId == workspaceId && i.AcceptedAtUtc == null && i.CancelledAtUtc == null)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public void Add(MemberInvitation invitation) => dbContext.MemberInvitations.Add(invitation);
}
