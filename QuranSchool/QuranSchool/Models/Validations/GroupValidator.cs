using FluentValidation;
using MongoDB.Bson;
using MongoDB.Entities;
using QuranSchool.Models.Request;

namespace QuranSchool.Models.Validations;

public class GroupValidator : AbstractValidator<GroupModel>
{
    public GroupValidator()
    {
        RuleFor(x => x.StartDate)
            .NotNull().NotEmpty().WithMessage("Group Start date cannot be empty or null!")
            .GreaterThanOrEqualTo(DateTime.Now.AddDays(-1))
            .WithMessage("Group starting date minimum value is yesterday");

        RuleFor(x => x.Name)
            .NotNull()
            .MinimumLength(3).WithMessage("Name is 3 characters minimum!")
            .MaximumLength(100).WithMessage("Group Name is 100 characters max!");

        RuleFor(x => x.Sessions)
            .NotNull()
            .NotEmpty()
            .Must(x => x is not null && x.Count != 0)
            .WithMessage("SessionModels cannot be empty!")
            .Must(sessions =>
            {
                try
                {
                    var sessionDates = sessions
                        .Select(x => (ObjectId.GenerateNewId().ToString(), x.Start, x.End))
                        .ToList();
                    var overlapped = sessionDates.Any(range => DateOverlap.IsOverlapped(range,
                        sessionDates.Where(x => x.End != range.Start && x.End != range.Start).ToArray()));

                    return !overlapped;
                }
                catch
                {
                    return false;
                }
            })
            .WithMessage("SessionModels Must not be overlapped!");

        RuleForEach(x => x.Students)
            .Must(id =>
            {
                try
                {
                    var exist = DB.Find<Student>()
                        .MatchID(id)
                        .ExecuteAnyAsync()
                        .GetAwaiter().GetResult();
                    return exist;
                }
                catch
                {
                    return false;
                }
            }).WithMessage("Invalid Student ID / Doesn't exist!");

        RuleFor(x => x.Teacher.ID)
            .Must(id =>
            {
                try
                {
                    var exist = DB.Find<Teacher>()
                        .MatchID(id)
                        .ExecuteAnyAsync()
                        .GetAwaiter().GetResult();
                    return exist;
                }
                catch
                {
                    return false;
                }
            }).WithMessage("Invalid Teacher ID / Doesn't Exist!");
        RuleFor(x => x.Formation.ID)
            .Must(id =>
            {
                try
                {
                    var exist = DB.Find<Formation>()
                        .MatchID(id)
                        .ExecuteAnyAsync()
                        .GetAwaiter().GetResult();
                    ;
                    return exist;
                }
                catch
                {
                    return false;
                }
            }).WithMessage("Invalid Formation ID / Doesn't Exist!");
    }
}