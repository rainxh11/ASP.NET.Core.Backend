using FluentValidation;

using MongoDB.Bson;
using MongoDB.Driver.Linq;
using MongoDB.Entities;

using QuranSchool.Models.Request;
using QuranSchool.Services;

namespace QuranSchool.Models.Validations;

public class ScheduleUpdateValidator : AbstractValidator<ScheduleUpdateModel>
{
    public ScheduleUpdateValidator(SessionService sessionService)
    {
        RuleFor(x => x.Id)
           .Must(x => DB.Find<Group>().MatchID(x).ExecuteAnyAsync().RunSync())
           .WithMessage("Group doesn't exist!")
           .WithErrorCode("404");

        RuleFor(x => x.Occurrences)
           .Must((model,
                occurrences) =>
            {
                try
                {
                    if (occurrences?.Count == 0 || occurrences is null) return true;

                    var sessions = sessionService.CreateSessions(occurrences, DateTime.Today,
                        ((model.Until ?? DateTime.Today) - DateTime.Today).Days);
                    var sessionDates = sessions
                                      .Select(x => (ObjectId.GenerateNewId().ToString(), x.Start, x.End))
                                      .ToList();
                    var overlapped = sessionDates
                       .Any(range => DateOverlap.IsOverlapped(range,
                            sessionDates
                               .Where(x => x.End != range.Start && x.End != range.Start)
                               .ToArray()));

                    model.SessionModels = sessions;
                    return !overlapped;
                }
                catch
                {
                    return false;
                }
            })
           .WithMessage("SessionModels Must not be overlapped!");
    }
}

public class GroupUpdateValidator : AbstractValidator<GroupUpdateModel>
{
    public GroupUpdateValidator(SessionService sessionService)
    {
        RuleFor(x => x.Name)
           .MaximumLength(100).WithMessage("Group Name is 100 characters max!");

        RuleFor(x => x.Occurrences)
           .Must((model,
                occurrences) =>
            {
                try
                {
                    var group = DB.Find<Group>().MatchID(model.GroupId).ExecuteFirstAsync().RunSync();
                    model.Group = group;
                    if (occurrences?.Count == 0 || occurrences is null) return true;

                    var formation = group.Formation;

                    var sessions = sessionService.CreateSessions(occurrences, group.Start.Date, formation.DurationDays);
                    var sessionDates = sessions
                                      .Select(x => (ObjectId.GenerateNewId().ToString(), x.Start, x.End))
                                      .ToList();
                    var overlapped = sessionDates
                       .Any(range => DateOverlap.IsOverlapped(range,
                            sessionDates
                               .Where(x => x.End != range.Start && x.End != range.Start)
                               .ToArray()));

                    model.SessionModels = sessions;
                    return !overlapped;
                }
                catch
                {
                    return false;
                }
            })
           .WithMessage("SessionModels Must not be overlapped!");


        RuleFor(x => x.Teacher)
           .Must(teacher =>
            {
                try
                {
                    if (teacher is null) return true;
                    var exist = DB.Find<Teacher>()
                                  .MatchID(teacher.ID)
                                  .ExecuteAnyAsync()
                                  .RunSync();
                    return exist;
                }
                catch
                {
                    return false;
                }
            }).WithMessage("Invalid Teacher ID / Doesn't Exist!");
    }
}

public class GroupCreateValidator : AbstractValidator<GroupCreateModel>
{
    public GroupCreateValidator(SessionService sessionService)
    {
        RuleFor(x => x.StartDate)
           .NotNull().NotEmpty().WithMessage("Group Start date cannot be empty or null!")
           .GreaterThanOrEqualTo(DateTime.Now.AddDays(-1))
           .WithMessage("Group starting date minimum value is yesterday");

        RuleFor(x => x.Name)
           .NotNull()
           .MinimumLength(3).WithMessage("Name is 3 characters minimum!")
           .MaximumLength(100).WithMessage("Group Name is 100 characters max!");

        RuleFor(x => x.Occurrences)
           .NotNull()
           .NotEmpty()
           .Must(x => x is not null && x.Count != 0)
           .WithMessage("SessionModels Dates & Time cannot be empty!")
           .Must((model,
                occurrences) =>
            {
                try
                {
                    var formation = DB.Find<Formation>().MatchID(model.Formation.ID).ExecuteSingleAsync().RunSync();

                    var sessions =
                        sessionService.CreateSessions(occurrences, model.StartDate.Date, formation.DurationDays);
                    var sessionDates = sessions
                                      .Select(x => (ObjectId.GenerateNewId().ToString(), x.Start, x.End))
                                      .ToList();
                    var overlapped = sessionDates.Any(range => DateOverlap.IsOverlapped(range,
                        sessionDates.Where(x => x.End != range.Start && x.End != range.Start).ToArray()));

                    model.Sessions = sessions;
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
                                  .RunSync();
                    return exist;
                }
                catch
                {
                    return false;
                }
            }).WithMessage("Invalid Student ID / Doesn't exist!");

        RuleFor(x => x.Teacher.ID)
           .NotNull()
           .NotEmpty()
           .Must(id =>
            {
                try
                {
                    var exist = DB.Find<Teacher>()
                                  .MatchID(id)
                                  .ExecuteAnyAsync()
                                  .RunSync();
                    return exist;
                }
                catch
                {
                    return false;
                }
            }).WithMessage("Invalid Teacher ID / Doesn't Exist!");
        RuleFor(x => x.Formation.ID)
           .NotNull()
           .NotEmpty()
           .Must(id =>
            {
                try
                {
                    var exist = DB.Find<Formation>()
                                  .MatchID(id)
                                  .ExecuteAnyAsync()
                                  .RunSync();
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