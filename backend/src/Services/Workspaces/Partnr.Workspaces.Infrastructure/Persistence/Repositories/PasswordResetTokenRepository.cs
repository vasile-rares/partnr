using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Partnr.Workspaces.Infrastructure.Persistence.Repositories;

internal sealed class PasswordResetTokenRepository(ApplicationDbContext dbContext) : IPasswordResetTokenRepository
{
    public Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public Task<PasswordResetToken?> GetOpenForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.PasswordResetTokens.FirstOrDefaultAsync(
            t => t.UserId == userId && t.UsedAtUtc == null && t.CancelledAtUtc == null,
            cancellationToken);

    public void Add(PasswordResetToken token) => dbContext.PasswordResetTokens.Add(token);
}
