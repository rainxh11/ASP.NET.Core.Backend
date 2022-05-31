using System.Linq;
using System.Text.RegularExpressions;
using FluentValidation;
using MongoDB.Bson;
using UATL.MailSystem.Common.Models;
using UATL.MailSystem.Common.Models.Request;

namespace UATL.MailSystem.Common.Validations;

public class SendMailRequestValidator : AbstractValidator<SendMailRequest>
{
    public SendMailRequestValidator()
    {
        RuleFor(x => x).Custom((x, validationContext) =>
        {
            if (x.Recipients.Count == 0 && x.Type == MailType.Internal)
                validationContext.AddFailure(nameof(x.Recipients), "Recipients cannot be empty if mail is internal.");
            if (x.Recipients.Any(r => !ObjectId.TryParse(r, out _)))
                validationContext.AddFailure(nameof(x.Recipients), "Recipients contain invalid IDs");
        });

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Draft Subject cannot be empty!")
            .MinimumLength(3).WithMessage("Draft Subject Minimum length is 3 characters!")
            .MaximumLength(100).WithMessage("Draft Subject is too long, maximum is 100 characters!");

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