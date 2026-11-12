using System.ComponentModel.DataAnnotations;

namespace Fundoo.User.Api.Contracts
{
    public class ResetPasswordRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Reset token is required.")]
        public string Token { get; set; } = "";

        [Required(ErrorMessage = "New password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        [RegularExpression(
            @"^(?=.*[A-Za-z])(?=.*\d)(?=.*[^A-Za-z\d\s])\S{8,}$",
            ErrorMessage = "Password must contain at least one letter, one digit, and one special character, with no spaces.")]
        public string NewPassword { get; set; } = "";
    }
}