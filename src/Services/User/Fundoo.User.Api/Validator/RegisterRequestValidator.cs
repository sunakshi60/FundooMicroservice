using FluentValidation;
using Fundoo.User.Api.Contracts;

namespace Fundoo.User.Api.Validators
{
    public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
    {
        public RegisterRequestValidator()
        {
            // First Name
            RuleFor(x => x.FirstName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("First name is required.")
                .Length(2, 50)
                .WithMessage("First name must be between 2 and 50 characters.")
                .Matches(@"^[A-Za-z]+(?:[ '-][A-Za-z]+)*$")
                .WithMessage("First name can contain only letters, spaces, hyphens, or apostrophes.");

            // Last Name
            RuleFor(x => x.LastName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("Last name is required.")
                .Length(2, 50)
                .WithMessage("Last name must be between 2 and 50 characters.")
                .Matches(@"^[A-Za-z]+(?:[ '-][A-Za-z]+)*$")
                .WithMessage("Last name can contain only letters, spaces, hyphens, or apostrophes.");

            // Email
            RuleFor(x => x.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("Email is required.")
                .EmailAddress()
                .WithMessage("Enter a valid email address.");

            // Password
            RuleFor(x => x.Password)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("Password is required.")
                .Matches(@"^(?=.*[A-Za-z])(?=.*\d)(?=.*[^A-Za-z\d\s])\S{8,}$")
                .WithMessage("Password must contain at least 8 characters, including one letter, one digit, and one special character, with no spaces.");
        }
    }
}