using System.Text.RegularExpressions;
using FluentValidation;
using UATL.MailSystem.Common.Models.Request;

namespace UATL.MailSystem.Common.Validations;

public class DraftValidator : AbstractValidator<DraftRequest>
{
    public DraftValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Draft Subject cannot be empty!")
            .MinimumLength(3).WithMessage("Draft Subject Minimum length is 3 characters!")
            .MaximumLength(100).WithMessage("Draft Subject is too long, maximum is 100 characters!");

        // HashTag regex (#+[a-zA-Z0-9(_)(\-)]{1,})
        RuleFor(x => x.HashTags)
            .Custom((tags, validator) =>
            {
                var regex = new Regex(@"(#+[a-zA-Z0-9(_)(\-)]{1,})",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                var whiteSpaceRegex = new Regex(@"[\s]");

                foreach (var hashTag in tags)
                {
                    if (whiteSpaceRegex.Match(hashTag).Success)
                        validator.AddFailure(hashTag, "HashTag should not contain any whitespaces!");
                    if (!regex.Match(hashTag).Success)
                        validator.AddFailure(hashTag, "Not a valid HashTag!");
                }
            });
    }
}

public class SendDraftValidator : AbstractValidator<SendDraftRequest>
{
    public SendDraftValidator()
    {
        RuleFor(x => x.Recipients)
            .NotEmpty().WithMessage("Specify at least one Recipient!");
    }
}