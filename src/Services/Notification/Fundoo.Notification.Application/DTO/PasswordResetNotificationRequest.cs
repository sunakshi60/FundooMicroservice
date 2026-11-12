using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.Notification.Application.DTO
{
    public sealed class PasswordResetNotificationRequest
    {
        public string RecipientEmail { get; set; } = string.Empty;

        public string RecipientName { get; set; } = string.Empty;   // NEW

        public string ResetToken { get; set; } = string.Empty;

    }
}
