using FluentValidation;
using QuranSchool.Models.Request;

namespace QuranSchool.Models.Validations;

public class FormationValidator : AbstractValidator<FormationModel>
{
    public FormationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Formation name cannot be empty!")
            .MinimumLength(3).WithMessage("Formation name length is 3 characters minimum!")
            .MaximumLength(100).WithMessage("Formation name length is 100 characters maximum!");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Formation price cannot be negative")
            .NotEmpty().NotNull();

        RuleFor(x => x.DurationDays)
            .GreaterThan(0).WithMessage("Formation days cannot be 0")
            .NotEmpty().NotNull();
    }
}