using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Application.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fundoo.Notification.Api.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [EnableRateLimiting("NotificationEmail")]
    public sealed class NotificationController(INotificationService notificationService) : ControllerBase
    {
        private readonly INotificationService _notificationService = notificationService;

        [HttpPost("password-reset")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SendPasswordReset([FromBody] PasswordResetNotificationRequest request,
        CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.RecipientEmail) ||
                string.IsNullOrWhiteSpace(request.ResetToken))
            {
                return BadRequest(new
                {
                    isSuccess = false,
                    message = "Recipient email and reset token are required."
                });
            }

            await _notificationService.SendPasswordResetNotificationAsync(request);

            return Accepted(new
            {
                isSuccess = true,
                message = "Password-reset notification accepted."
            });
        }

        [HttpPost("welcome")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SendWelcome([FromBody] WelcomeNotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RecipientEmail) ||
                string.IsNullOrWhiteSpace(request.RecipientName))
            {
                return BadRequest(new
                {
                    isSuccess = false,
                    message = "Recipient email and name are required."
                });
            }

            await _notificationService.SendWelcomeNotificationAsync(request);

            return Accepted(new
            {
                isSuccess = true,
                message = "Welcome notification accepted."
            });
        }
    }
}
