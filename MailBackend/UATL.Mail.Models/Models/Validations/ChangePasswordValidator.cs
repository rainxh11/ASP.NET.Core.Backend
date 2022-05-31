using FluentValidation;
using UATL.MailSystem.Common.Request;

namespace UATL.MailSystem.Common.Validations;

public class ChangePasswordValidator : AbstractValidator<ChangePasswordModel>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.OldPassword)
            .NotEmpty().WithMessage("Old Password cannot be empty!")
            .MinimumLength(8).WithMessage("Password length is 8 characters minimum!")
            .MaximumLength(20).WithMessage("Password maximum length is 20 characters!");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Password cannot be empty!")
            .MinimumLength(8).WithMessage("Password length is 8 characters minimum!")
            .MaximumLength(20).WithMessage("Password maximum length is 20 characters!");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Password cannot be empty!")
            .MinimumLength(8).WithMessage("Password length is 8 characters minimum!")
            .Equal(x => x.ConfirmPassword).WithMessage("Password & Confirmation are not equal!")
            .MaximumLength(20).WithMessage("Password maximum length is 20 characters!")
            .NotEqual(x => x.OldPassword).WithMessage("New Password cannot be the same as the Old Password!");
    }
}