using FluentValidation;
using UATL.MailSystem.Common.Request;

namespace UATL.MailSystem.Common.Validations;

public class LoginValidator : AbstractValidator<LoginModel>
{
    public LoginValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().WithMessage("Username cannot be empty!")
            .NotNull().WithMessage("Username cannot be null!")
            .MinimumLength(5).WithMessage("Username minimum length is 5 characters!")
            .Matches(@"^[a-zA-Z0-9]*$").WithMessage("Special characters & empty spaces are not allowed in Username!");


        RuleFor(x => x.Password).NotEmpty().WithMessage("Password cannot be empty!")
            .NotNull().WithMessage("Password cannot be null!")
            .MinimumLength(8).WithMessage("Password minimum length is 8 characters!")
            .MaximumLength(20).WithMessage("Password maximum length is 20 characters!");
    }
}