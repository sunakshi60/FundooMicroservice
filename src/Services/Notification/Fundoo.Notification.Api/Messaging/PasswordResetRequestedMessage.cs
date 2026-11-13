using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.User.Infrastructure.Messaging
{
    public class PasswordResetRequestedMessage
    {
        public string EventId { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string ResetToken { get; set; } = string.Empty;

        public DateTime RequestedAtUtc { get; set; }
    }
}
