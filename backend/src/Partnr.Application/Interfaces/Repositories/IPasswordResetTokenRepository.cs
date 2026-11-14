using Partnr.Domain.Entities;

namespace Partnr.Application.Interfaces.Repositories;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>The account's link that is neither used nor cancelled (it may have expired).</summary>
    Task<PasswordResetToken?> GetOpenForUserAsync(Guid userId, CancellationToken cancellationToken);

    void Add(PasswordResetToken token);
}
