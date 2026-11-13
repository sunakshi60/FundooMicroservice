using Fundoo.User.Application.Abstractions;
using Fundoo.User.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fundoo.User.Infrastructure.Security
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddUserInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("UserDb")
                ?? throw new InvalidOperationException(
                    "UserDb connection string is not configured.");

            services.AddDbContext<UserDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
                options.UseOpenIddict();
            });
            services.AddSingleton<KafkaProducer>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
            services.AddScoped<ITokenService, JwtTokenService>();

            services.AddScoped<IAuthService, AuthService>();

            return services;
        }
    }
}
