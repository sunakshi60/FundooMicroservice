
namespace Fundoo.User.Infrastructure.Security
{
    public sealed class PasswordResetNotificationRequest
    {
        public string RecipientEmail { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public string ResetToken { get; set; } = string.Empty;
    }
}
