using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.Notification.Application.DTO
{
    public class LoginOtpNotificationRequest
    {
        public string RecipientEmail { get; set; } = string.Empty;

        public string RecipientName { get; set; } = string.Empty;

        public string Otp { get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }
    }
}
