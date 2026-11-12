using Fundoo.User.Domain.Entity;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.User.Infrastructure.Security
{
    public class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
    {
        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<AppUser>(e =>
            {
                e.HasKey(user => user.Id);
                e.HasIndex(u => u.NormalizedEmail).IsUnique();   // DB-level uniqueness
                e.Property(u => u.Email).HasMaxLength(256);
                e.Property(u => u.NormalizedEmail).HasMaxLength(256);
            });
            b.Entity<PasswordResetToken>(e =>
            {
                e.HasKey(token => token.Id);

                e.Property(token => token.TokenHash)
                    .IsRequired()
                    .HasMaxLength(64);

                e.HasIndex(token => token.TokenHash)
                    .IsUnique();

                e.HasIndex(token => token.UserId);

                e.Property(token => token.ExpiresAtUtc)
                    .IsRequired();

                e.HasOne<AppUser>()
                    .WithMany()
                    .HasForeignKey(token => token.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

        }
    }
}
