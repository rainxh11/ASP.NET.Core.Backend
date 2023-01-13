using FluentValidation;
using MongoDB.Entities;
using QuranSchool.Models.Request;

namespace QuranSchool.Models.Validations;

public class StudentPresenceValidator : AbstractValidator<StudentPresenceModel>
{
    public StudentPresenceValidator()
    {
        RuleFor(x => x.Student)
            .Must(id =>
            {
                try
                {
                    var exist = DB.Find<Student>()
                        .MatchID(id)
                        .ExecuteAnyAsync()
                        .GetAwaiter()
                        .GetResult();
                    return exist;
                }
                catch
                {
                    return false;
                }
            }).WithMessage("Student ID Invalid / NotFound");
    }
}