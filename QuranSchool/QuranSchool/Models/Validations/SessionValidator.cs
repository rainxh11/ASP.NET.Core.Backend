using FluentValidation;
using QuranSchool.Models.Request;

namespace QuranSchool.Models.Validations;

public class SessionValidator : AbstractValidator<SessionModel>
{
    public SessionValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Start < x.End)
            .WithMessage("Start time must be earlier than End time!");
    }
}