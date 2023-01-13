using FluentValidation;
using MongoDB.Entities;
using QuranSchool.Models.Request;

namespace QuranSchool.Models.Validations;

public class StudentValidator : AbstractValidator<StudentModel>
{
    public StudentValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Student Name Cannot be empty!")
            .MinimumLength(3).WithMessage("Student Name Minimum length is 3 characters!")
            .MaximumLength(100).WithMessage("Maximum length of Student Name is 100 characters");
        //.Matches(@"^[u0621-\u064Aa-zA-Z\s]*$")
        //.WithMessage("Special characters & digits are not allowed in Account Name!");

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateTime.Now.AddYears(-1))
            .WithMessage("Student Age must be more than 1 years")
            .GreaterThan(DateTime.Now.AddYears(-100))
            .WithMessage("Student Age must be less than 100 years");

        /*RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone Number cannot be empty")
            .Matches(@"^0(\d| ){8,20}").WithMessage("Not a valid phone number");*/

        RuleForEach(x => x.Groups)
            .Must(id =>
            {
                try
                {
                    var exist = DB.Find<Group>()
                        .MatchID(id)
                        .ExecuteAnyAsync()
                        .GetAwaiter().GetResult();
                    return exist;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }).WithMessage("Invalid Group ID / Doesn't exist!");
    }
}