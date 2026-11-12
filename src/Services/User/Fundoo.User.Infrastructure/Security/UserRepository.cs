using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.User.Infrastructure.Security
{
    public class UserRepository : IUserRepository
    {
        private readonly UserDbContext _context;

        public UserRepository(UserDbContext context)
        {
            _context = context;
        }

        public async Task<AppUser?> GetByNormalizedEmailAsync(string normalizedEmail,
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .FirstOrDefaultAsync(
                    user => user.NormalizedEmail == normalizedEmail,
                    cancellationToken);
        }

        public async Task<AppUser?> GetByIdAsync(Guid id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .FirstOrDefaultAsync(
                    user => user.Id == id,
                    cancellationToken);
        }

        public async Task AddAsync(AppUser user,
            CancellationToken cancellationToken = default)
        {
            await _context.Users.AddAsync(user, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task AddPasswordResetTokenAsync(PasswordResetToken token,
            CancellationToken cancellationToken = default)
        {
            await _context.PasswordResetTokens.AddAsync(
                token,
                cancellationToken);
        }

        public async Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default)
        {
            return await _context.PasswordResetTokens
                .FirstOrDefaultAsync(
                    token => token.TokenHash == tokenHash,
                    cancellationToken);
        }

        public async Task InvalidatePasswordResetTokensAsync(Guid userId,
            DateTime usedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var tokens = await _context.PasswordResetTokens
                .Where(token =>
                    token.UserId == userId &&
                    token.UsedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var token in tokens)
            {
                token.UsedAtUtc = usedAtUtc;
            }
        }

        public async Task UpdatePasswordAsync(AppUser user, string passwordHash,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            user.PasswordHash = passwordHash;
            user.UpdatedAtUtc = updatedAtUtc;

            _context.Users.Update(user);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
