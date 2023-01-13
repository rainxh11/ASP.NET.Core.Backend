using FluentValidation;
using MongoDB.Entities;
using QuranSchool.Models.Request;

namespace QuranSchool.Models.Validations;

public class SessionCreateValidator : AbstractValidator<SessionCreateModel>
{
    public SessionCreateValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Start < x.End)
            .WithMessage("Start time must be earlier than End time!");
        RuleFor(x => x.Teacher)
            .Must(teacher =>
            {
                try
                {
                    if (teacher == null) return true;

                    var exist = DB.Find<Teacher>()
                        .MatchID(teacher.ID)
                        .ExecuteAnyAsync()
                        .GetAwaiter().GetResult();
                    return exist;
                }
                catch
                {
                    return false;
                }
            }).WithMessage("Invalid Teacher ID / Doesn't Exist!");
    }
}