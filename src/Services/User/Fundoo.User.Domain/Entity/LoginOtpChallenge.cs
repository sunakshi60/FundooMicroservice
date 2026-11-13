using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.User.Domain.Entity
{
    public class LoginOtpChallenge
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public string OtpHash { get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? UsedAtUtc { get; set; }

        public int FailedAttempts { get; set; }

        public bool IsUsed { get; set; }
    }
}
