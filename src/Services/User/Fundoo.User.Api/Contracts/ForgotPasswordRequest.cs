using System.ComponentModel.DataAnnotations;

namespace Fundoo.User.Api.Contracts
{
    public sealed class ForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }
}
