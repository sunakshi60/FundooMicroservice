using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entity;
using Microsoft.Extensions.Logging;
using Polly.RateLimit;
using Polly.Timeout;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace Fundoo.User.Infrastructure.Security
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenService tokenService, IHttpClientFactory httpClientFactory, ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<AuthResult> RegisterAsync(string firstName, string lastName, string email, string password,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var existingUser =  await _userRepository.GetByNormalizedEmailAsync(
                    normalizedEmail,
                    cancellationToken);

            if (existingUser is not null)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Email is already registered."
                };
            }

            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Email = email.Trim(),
                NormalizedEmail = normalizedEmail,
                PasswordHash = _passwordHasher.Hash(password),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);

            var accessToken = _tokenService.CreateAccessToken(user);

            return new AuthResult
            {
                IsSuccess = true,
                Message = "Registration successful.",
                UserId = user.Id
            };
        }

        public async Task<AuthResult> LoginAsync(string email, string password,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var user =
                await _userRepository.GetByNormalizedEmailAsync(
                    normalizedEmail,
                    cancellationToken);

            if (user is null ||
                !user.IsActive ||
                !_passwordHasher.Verify(password, user.PasswordHash))
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Invalid email or password."
                };
            }

            var accessToken = _tokenService.CreateAccessToken(user);

            return new AuthResult
            {
                IsSuccess = true,
                Message = "Login successful.",
                AccessToken = accessToken,
                UserId = user.Id
            };
        }

        public async Task ForgotPasswordAsync(string email,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var user = await _userRepository.GetByNormalizedEmailAsync(
                normalizedEmail,
                cancellationToken);

            if (user is null || !user.IsActive)
                return;

            var rawToken = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32));

            var tokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

            var now = DateTime.UtcNow;

            // Invalidate older, unused reset tokens.
            await _userRepository.InvalidatePasswordResetTokensAsync(
                user.Id,
                now,
                cancellationToken);

            var resetToken = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAtUtc = now.AddMinutes(15),
                UsedAtUtc = null
            };

            await _userRepository.AddPasswordResetTokenAsync(
                resetToken,
                cancellationToken);

            await _userRepository.SaveChangesAsync(cancellationToken);

            try
            {
                var httpClient = _httpClientFactory.CreateClient("NotificationService");

                var notificationRequest = new PasswordResetNotificationRequest
                {
                    RecipientEmail = user.Email,
                    RecipientName = user.FirstName,
                    ResetToken = rawToken
                };

                using var response = await httpClient.PostAsJsonAsync(
                    "api/notifications/password-reset",
                    notificationRequest,
                    cancellationToken);

                _logger.LogInformation(
                    "Notification API returned HTTP {StatusCode} for user {UserId}.",
                    (int)response.StatusCode, user.Id);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning(
                        "Notification API rate limit reached for user {UserId}. " +
                        "The password-reset email was not accepted.",
                        user.Id);
                }
                else if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Notification API returned HTTP {StatusCode} for user {UserId}.",
                        (int)response.StatusCode,
                        user.Id);
                }
                else
                {
                    _logger.LogInformation(
                        "Password-reset notification accepted by Notification API for user {UserId}.",
                        user.Id);
                }
            }
            catch (TimeoutRejectedException ex)
            {
                _logger.LogError(
                    ex,
                    "Notification API timed out while sending a password-reset email for user {UserId}.",
                    user.Id);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(
                    ex,
                    "Could not communicate with Notification API for user {UserId}.",
                    user.Id);
            }
            catch (RateLimitRejectedException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Notification call rejected by the outbound concurrency limiter " +
                    "for user {UserId}.",
                    user.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error while sending a password-reset email for user {UserId}.",
                    user.Id);
            }
        }
           
        public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var user = await _userRepository.GetByNormalizedEmailAsync(
                normalizedEmail,
                cancellationToken);

            if (user is null || !user.IsActive)
                return false;

            if (string.IsNullOrWhiteSpace(token) ||
                string.IsNullOrWhiteSpace(newPassword))
            {
                return false;
            }

            var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

            var resetToken = await _userRepository.GetPasswordResetTokenByHashAsync(
                    tokenHash,
                    cancellationToken);

            var now = DateTime.UtcNow;

            if (resetToken is null ||
                resetToken.UserId != user.Id ||
                resetToken.UsedAtUtc is not null ||
                resetToken.ExpiresAtUtc <= now)
            {
                return false;
            }

            var newPasswordHash = _passwordHasher.Hash(newPassword);

            await _userRepository.UpdatePasswordAsync(
                user,
                newPasswordHash,
                now,
                cancellationToken);

            resetToken.UsedAtUtc = now;

            await _userRepository.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<AuthResult> GoogleLoginAsync(string email,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var user = await _userRepository.GetByNormalizedEmailAsync(
                normalizedEmail,
                cancellationToken);

            if (user is null || !user.IsActive)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "User is not registered or inactive."
                };
            }

            var accessToken = _tokenService.CreateAccessToken(user);

            return new AuthResult
            {
                IsSuccess = true,
                Message = "Google login successful.",
                UserId = user.Id,
                AccessToken = accessToken
            };
        }
    }
}