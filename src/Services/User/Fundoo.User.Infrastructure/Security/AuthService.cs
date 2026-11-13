using Fundoo.User.Application.Abstractions;
using Fundoo.User.Application.Events;
using Fundoo.User.Domain.Entity;
using Fundoo.User.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Fundoo.User.Infrastructure.Security
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthService> _logger;
        private readonly KafkaProducer _kafkaProducer;
        private readonly IConfiguration _configuration;

        public AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenService tokenService, KafkaProducer kafkaProducer, ILogger<AuthService> logger, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _kafkaProducer = kafkaProducer;
            _logger = logger;
            _configuration = configuration;
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

        public async Task<AuthResult> LoginAsync(
    string email,
    string password,
    CancellationToken cancellationToken = default)
        {
            // STEP 1: Find user using normalized email.
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var user = await _userRepository.GetByNormalizedEmailAsync(
                normalizedEmail,
                cancellationToken);

            // STEP 2: Verify credentials.
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

            // STEP 3: Generate cryptographically secure six-digit OTP.
            var otp = RandomNumberGenerator.GetInt32(0, 1_000_000)
                .ToString("D6");

            // STEP 4: Read HMAC secret from User Secrets.
            var secret = _configuration["OtpSettings:HmacSecret"];

            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new InvalidOperationException(
                    "OTP HMAC secret is not configured.");
            }

            // STEP 5: Generate HMAC-SHA256 hash.
            using var hmac = new HMACSHA256(
                Encoding.UTF8.GetBytes(secret));

            var otpHash = Convert.ToHexString(
                hmac.ComputeHash(Encoding.UTF8.GetBytes(otp)));

            // STEP 6: Create OTP challenge.
            var now = DateTime.UtcNow;
            var challengeId = Guid.NewGuid();
            var expiresAt = now.AddMinutes(5);

            // STEP 7: Invalidate previous unused OTP challenges.
            await _userRepository.InvalidateLoginOtpChallengesAsync(
                user.Id,
                now,
                cancellationToken);

            // STEP 8: Create new challenge.
            var challenge = new LoginOtpChallenge
            {
                Id = challengeId,
                UserId = user.Id,
                OtpHash = otpHash,
                CreatedAtUtc = now,
                ExpiresAtUtc = expiresAt,
                FailedAttempts = 0,
                IsUsed = false
            };

            // STEP 9: Save challenge in PostgreSQL.
            await _userRepository.AddLoginOtpChallengeAsync(
                challenge,
                cancellationToken);

            await _userRepository.SaveChangesAsync(cancellationToken);

            // STEP 10: Publish OTP event to Kafka.
            try
            {
                var otpEvent = new LoginOtpRequested
                {
                    EventId = Guid.NewGuid().ToString(),
                    UserId = user.Id.ToString(),
                    Email = user.Email,
                    UserName = $"{user.FirstName} {user.LastName}".Trim(),
                    Otp = otp,
                    ExpiresAtUtc = expiresAt,
                    RequestedAtUtc = now
                };

                await _kafkaProducer.PublishLoginOtpAsync(
                    otpEvent,
                    cancellationToken);

                _logger.LogInformation(
                    "Login OTP event published for user {UserId}.",
                    user.Id);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to publish login OTP event for user {UserId}.",
                    user.Id);

                // Invalidate the challenge because email delivery failed.
                await _userRepository.InvalidateLoginOtpChallengesAsync(
                    user.Id,
                    DateTime.UtcNow,
                    cancellationToken);

                await _userRepository.SaveChangesAsync(cancellationToken);

                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Unable to send verification code. Please try again."
                };
            }

            // STEP 11: Return challenge ID, NOT JWT.
            return new AuthResult
            {
                IsSuccess = true,
                Message = "OTP sent to your registered email.",
                RequiresTwoFactor = true,
                ChallengeId = challengeId
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
                var passwordResetEvent = new PasswordResetRequested
                {
                    EventId = Guid.NewGuid().ToString(),
                    UserId = user.Id.ToString(),
                    Email = user.Email,
                    UserName = $"{user.FirstName} {user.LastName}".Trim(),
                    ResetToken = rawToken,
                    RequestedAtUtc = DateTime.UtcNow
                };

                await _kafkaProducer.PublishPasswordResetAsync(
                    passwordResetEvent,
                    cancellationToken);

                _logger.LogInformation(
                    "Password reset event published for user {UserId}.",
                    user.Id);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to publish password reset event for user {UserId}.",
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


        public async Task<AuthResult> VerifyLoginOtpAsync(
            Guid challengeId,
            string otp,
            CancellationToken cancellationToken = default)
        {
            // STEP 1: Validate OTP input.
            if (string.IsNullOrWhiteSpace(otp) ||
                otp.Length != 6 ||
                !otp.All(char.IsDigit))
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Invalid OTP."
                };
            }

            // STEP 2: Find the OTP challenge.
            var challenge = await _userRepository.GetLoginOtpChallengeByIdAsync(
                challengeId,
                cancellationToken);

            if (challenge is null || challenge.IsUsed)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Invalid or already used OTP challenge."
                };
            }

            var now = DateTime.UtcNow;

            // STEP 3: Check OTP expiry.
            if (challenge.ExpiresAtUtc <= now)
            {
                challenge.IsUsed = true;
                challenge.UsedAtUtc = now;

                await _userRepository.SaveChangesAsync(cancellationToken);

                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "OTP has expired. Please login again."
                };
            }

            // STEP 4: Check maximum failed attempts.
            const int maxAttempts = 5;

            if (challenge.FailedAttempts >= maxAttempts)
            {
                challenge.IsUsed = true;
                challenge.UsedAtUtc = now;

                await _userRepository.SaveChangesAsync(cancellationToken);

                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Maximum OTP attempts exceeded. Please login again."
                };
            }

            // STEP 5: Generate HMAC hash for submitted OTP.
            var secret = _configuration["OtpSettings:HmacSecret"];

            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new InvalidOperationException(
                    "OTP HMAC secret is not configured.");
            }

            using var hmac = new HMACSHA256(
                Encoding.UTF8.GetBytes(secret));

            var submittedHash = hmac.ComputeHash(
                Encoding.UTF8.GetBytes(otp));

            var storedHash = Convert.FromHexString(challenge.OtpHash);

            // STEP 6: Compare hashes securely.
            if (!CryptographicOperations.FixedTimeEquals(
                submittedHash,
                storedHash))
            {
                challenge.FailedAttempts++;

                if (challenge.FailedAttempts >= maxAttempts)
                {
                    challenge.IsUsed = true;
                    challenge.UsedAtUtc = now;
                }

                await _userRepository.SaveChangesAsync(cancellationToken);

                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Invalid OTP."
                };
            }

            // STEP 7: Fetch the user.
            var user = await _userRepository.GetByIdAsync(
                challenge.UserId,
                cancellationToken);

            if (user is null || !user.IsActive)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "User is not registered or inactive."
                };
            }

            // STEP 8: Mark OTP as used.
            challenge.IsUsed = true;
            challenge.UsedAtUtc = now;

            await _userRepository.SaveChangesAsync(cancellationToken);

            // STEP 9: Generate JWT only after successful OTP verification.
            var accessToken = _tokenService.CreateAccessToken(user);

            _logger.LogInformation(
                "Two-factor login completed for user {UserId}.",
                user.Id);

            return new AuthResult
            {
                IsSuccess = true,
                Message = "Login successful.",
                UserId = user.Id,
                AccessToken = accessToken,
                RequiresTwoFactor = false
            };
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