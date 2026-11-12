using Fundoo.User.Domain.Entity;


namespace Fundoo.User.Application.Abstractions
{
    public interface IUserRepository
    {
        Task<AppUser?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

        Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task AddAsync(AppUser user, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);

        Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

        Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(string tokenHash,
            CancellationToken cancellationToken = default);

        Task InvalidatePasswordResetTokensAsync(Guid userId, DateTime usedAtUtc,
            CancellationToken cancellationToken = default);

        Task UpdatePasswordAsync(AppUser user, string passwordHash,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default);
    }
}
