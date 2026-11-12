
namespace Fundoo.User.Api.Contracts
{
    internal static class PasswordRules
    {
        // BCrypt sirf pehle 72 bytes use karta hai, isliye max 72 rakha hai.
        public const int MinLength = 8;
        public const int MaxLength = 72;

        public const string ComplexityPattern =
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\w\s]).+$";

        public const string ComplexityMessage =
            "Password must contain at least one uppercase letter, one lowercase letter, one digit and one special character.";

        public const string LengthMessage =
            "Password must be between 8 and 72 characters.";
    }
}

