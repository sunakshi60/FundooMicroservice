using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.User.Application.Events
{
    public class LoginOtpRequested
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        public string UserId { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Otp { get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
