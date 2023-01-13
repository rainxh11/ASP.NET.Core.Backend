using FluentValidation;
using QuranSchool.Models.Request;

namespace QuranSchool.Models.Validations;

public class TeacherValidator : AbstractValidator<TeacherModel>
{
    public TeacherValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().NotNull()
            .WithMessage("Teacher Name Cannot be empty or null!")
            .MinimumLength(3).WithMessage("Teacher Name length is 3 characters minimum");

        /*RuleForEach(x => x.PhoneNumbers)
            .NotEmpty().WithMessage("Phone Number cannot be empty")
            .Matches(@"^0(\d| ){8,20}").WithMessage("Not a valid phone number");*/

        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(DateTime.Now.AddYears(15))
            .WithMessage("Teacher Age must be at least 15 years old!")
            .NotNull().NotEmpty()
            .WithMessage("Teacher Date of birth cannot be null or empty!");
    }
}