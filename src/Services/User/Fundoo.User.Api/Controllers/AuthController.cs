using Microsoft.AspNetCore.Mvc;
using Fundoo.User.Api.Contracts;
using Fundoo.User.Application.Abstractions;
using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Google.Apis.Auth;

namespace Fundoo.User.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IValidator<RegisterRequest> _registerValidator;

        public AuthController(IAuthService authService, IValidator<RegisterRequest> registerValidator)
        {
            _authService = authService;
            _registerValidator = registerValidator;
        }

        [AllowAnonymous]
        [HttpGet("google-login")]
        public IActionResult GoogleLogin()
        {
            var callbackUrl = Url.Action(
                nameof(GoogleCallback),
                "Auth",
                new { version = "1.0" });

            var properties = new AuthenticationProperties
            {
                RedirectUri = callbackUrl
            };

            return Challenge(properties, "Google");
        }

        [AllowAnonymous]
        [HttpGet("google-callback")]
        public async Task<IActionResult> GoogleCallback(
            CancellationToken cancellationToken)
        {
            // Step 1: Read the temporary external authentication cookie.
            var externalResult = await HttpContext.AuthenticateAsync("External");

            if (!externalResult.Succeeded ||
                externalResult.Principal is null)
            {
                await HttpContext.SignOutAsync("External");

                return Unauthorized(new
                {
                    isSuccess = false,
                    message = "Google authentication failed."
                });
            }

            var principal = externalResult.Principal;

            // Step 2: Get the authenticated Google email.
            var email = principal.FindFirst("email")?.Value
                ?? principal.FindFirst(ClaimTypes.Email)?.Value;

            // Step 3: Verify that Google has verified this email.
            var emailVerified = principal.FindFirst("email_verified")?.Value;

            if (string.IsNullOrWhiteSpace(email) ||
                !bool.TryParse(emailVerified, out var isVerified) ||
                !isVerified)
            {
                await HttpContext.SignOutAsync("External");

                return Unauthorized(new
                {
                    isSuccess = false,
                    message = "Google email is missing or not verified."
                });
            }

            try
            {
                // Step 4: Check whether the user exists in Fundoo.
                var result = await _authService.GoogleLoginAsync(
                    email,
                    cancellationToken);

                if (!result.IsSuccess)
                {
                    return Unauthorized(new
                    {
                        isSuccess = false,
                        message = result.Message
                    });
                }

                // Step 5: Return Fundoo JWT.
                return Ok(new
                {
                    isSuccess = true,
                    message = result.Message,
                    userId = result.UserId,
                    accessToken = result.AccessToken,
                    tokenType = "Bearer"
                });
            }
            finally
            {
                // Step 6: Remove the temporary external cookie.
                await HttpContext.SignOutAsync("External");
            }
        }


        [AllowAnonymous]
        [HttpPost("google-token-login")]
        public async Task<IActionResult> GoogleTokenLogin([FromBody] GoogleTokenRequest request,
            CancellationToken cancellationToken)
        {
            // Step 1: Read Google Client ID from configuration.
            var clientId = HttpContext.RequestServices
                .GetRequiredService<IConfiguration>()["Authentication:Google:ClientId"];

            if (string.IsNullOrWhiteSpace(clientId))
            {
                return StatusCode(500, new
                {
                    isSuccess = false,
                    message = "Google Client ID is not configured."
                });
            }

            GoogleJsonWebSignature.Payload payload;

            // Step 2: Validate Google's ID token.
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(
                    request.IdToken,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = new[] { clientId }
                    });
            }
            catch (InvalidJwtException)
            {
                return Unauthorized(new
                {
                    isSuccess = false,
                    message = "Invalid Google ID token."
                });
            }

            // Step 3: Check verified email.
            if (string.IsNullOrWhiteSpace(payload.Email) ||
                payload.EmailVerified != true)
            {
                return Unauthorized(new
                {
                    isSuccess = false,
                    message = "Google email is missing or not verified."
                });
            }

            // Step 4: Check whether the user is registered in Fundoo.
            var result = await _authService.GoogleLoginAsync(
                payload.Email,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return Unauthorized(new
                {
                    isSuccess = false,
                    message = result.Message
                });
            }

            // Step 5: Return Fundoo JWT.
            return Ok(new
            {
                isSuccess = true,
                message = result.Message,
                userId = result.UserId,
                accessToken = result.AccessToken,
                tokenType = "Bearer"
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(
    [FromBody] RegisterRequest request,
    CancellationToken cancellationToken)
        {
            // Step 1: Validate registration request
            var validationResult = await _registerValidator.ValidateAsync(
                request,
                cancellationToken);

            // Step 2: If validation fails, return 400 Bad Request
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.ErrorMessage).ToArray()
                    );

                return BadRequest(new
                {
                    isSuccess = false,
                    message = "Validation failed.",
                    errors
                });
            }

            // Step 3: If validation succeeds, continue your existing flow
            var result = await _authService.RegisterAsync(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Password,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return Conflict(new
                {
                    isSuccess = false,
                    message = result.Message
                });
            }

            return StatusCode(StatusCodes.Status201Created, new
            {
                isSuccess = true,
                message = result.Message
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _authService.LoginAsync(
                request.Email,
                request.Password,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return Unauthorized(new
                {
                    isSuccess = false,
                    message = result.Message
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = result.Message,
                userId = result.UserId,
                accessToken = result.AccessToken,
                tokenType = "Bearer",
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request,
            CancellationToken cancellationToken)
        {
            await _authService.ForgotPasswordAsync(
                request.Email,
                cancellationToken);

            return Ok(new
            {
                isSuccess = true,
                message = "If the account exists, password reset instructions will be sent to the registered email."
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request,
            CancellationToken cancellationToken)
        {
            var isReset = await _authService.ResetPasswordAsync(
                request.Email,
                request.Token,
                request.NewPassword,
                cancellationToken);

            if (!isReset)
            {
                return BadRequest(new
                {
                    isSuccess = false,
                    message = "The password reset request is invalid or has expired."
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = "Password has been reset successfully."
            });
        }
    }
}