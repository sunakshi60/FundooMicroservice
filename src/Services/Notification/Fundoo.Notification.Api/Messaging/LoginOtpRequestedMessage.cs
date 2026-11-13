namespace Fundoo.Notification.Api.Messaging
{
    public class LoginOtpRequestedMessage
    {
        public string EventId { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Otp { get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime RequestedAtUtc { get; set; }
    }
}
