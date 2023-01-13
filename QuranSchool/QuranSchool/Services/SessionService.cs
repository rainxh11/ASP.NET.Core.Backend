using Dates.Recurring;
using Dates.Recurring.Type;

using Jetsons.JetPack;

using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Entities;

using QuranSchool.Models;
using QuranSchool.Models.Request;

namespace QuranSchool.Services;

public class SessionService
{
    private readonly IServiceProvider serviceProvider;
    private ILogger<SessionService> _logger;

    public SessionService(IServiceProvider serviceProvider,
        ILogger<SessionService> logger)
    {
        _logger = logger;
        this.serviceProvider = serviceProvider;
    }

    public async Task CleanDuplicateGroupSessions(string groupId,
        IClientSessionHandle session = null,
        CancellationToken ct = default)
    {
        try
        {
            var group = await DB.Find<Group>(session).MatchID(groupId).ExecuteSingleAsync(ct);

            group.Sessions = group.Sessions
                                  .Distinct(new SessionEqualityComparer())
                                  .GroupBy(x => x.Start.Date)
                                  .Select(x => x.First())
                                  .ToList();
            await group.SaveAsync(session, ct);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception.Message, exception.InnerException?.Message);
        }
    }

    public void TryDelegate(Delegate action)
    {
        var parameters = action.Method
                               .GetParameters()
                               .Where(x => (x.ParameterType.IsValueType && x.HasDefaultValue) ||
                                           !x.ParameterType.IsValueType)
                               .Select(x =>
                                    x.HasDefaultValue ? x.DefaultValue : serviceProvider.GetService(x.ParameterType))
                               .ToArray();
        var result = action.DynamicInvoke(parameters.Append(1955).ToArray());

        var x = result.GetType();
        var z = x;
    }


    public async Task<List<BsonDocument>> RetrieveSessions(
        Func<IAggregateFluent<Group>, IAggregateFluent<Group>> configure,
        DateTime? start,
        DateTime? end,
        CancellationToken ct)
    {
        var fluent = await configure(DB.Fluent<Group>())
                          .Unwind(x => x.Sessions)
                          .Project(new BsonDocument
                           {
                               { "Start", "$Sessions.Start" },
                               { "End", "$Sessions.End" },
                               { "Formation", "$Formation.Name" },
                               {
                                   "Teacher", new BsonDocument("$ifNull", new BsonArray
                                   {
                                       "$Sessions.Teacher.Name",
                                       "$Teacher.Name"
                                   })
                               },
                               { "Name", "$Name" },
                               { "Session", "$Session" },
                               { "ID", "$Sessions.ID" }
                           })
                          .Match(new BsonDocument()
                           {
                               { "Start", new BsonDocument("$gte", new BsonDateTime((DateTime)start)) },
                               { "End", new BsonDocument("$lte", new BsonDateTime((DateTime)end)) }
                           })
                          .ToListAsync(ct);

        return fluent;
    }

    public List<SessionModel> CreateSessions(List<OccurrenceModel> occurrences,
        DateTime start,
        int count)
    {
        var sessions = occurrences
           .SelectMany(occ =>
            {
                var weekly = Recurs
                            .Starting(start)
                            .Every(1)
                            .Weeks()
                            .OnDay(occ.Day)
                            .Ending(start.AddDays(count))
                            .Build();
                var dates = GetOccurrences(weekly, start)
                   .Select(d => new SessionModel
                    {
                        Start = d
                               .AddHours(occ.StartTime.Split(':')[0].ToInt())
                               .AddMinutes(occ.StartTime.Split(':')[1].ToInt()),
                        End = d
                             .AddHours(occ.EndTime.Split(':')[0].ToInt())
                             .AddMinutes(occ.EndTime.Split(':')[1].ToInt())
                    });
                return dates;
            });

        return sessions.ToList();
    }

    private List<DateTime> GetOccurrences(Weekly weekly,
        DateTime start)
    {
        List<DateTime> dates = new();
        DateTime? prevData = start;
        while ((prevData = weekly.Next(prevData.Value)).HasValue) dates.Add(prevData.Value);

        return dates;
    }

    public async Task AddGroupSessionsToStudents(IEnumerable<Student> students,
        Group group,
        CancellationToken ct = default,
        IClientSessionHandle? session = null)

    {
        var sessions = group.Sessions.Where(x =>
            x.Status is not SessionStatus.Cancelled and not SessionStatus.TeacherAbsent);

        students = students
                  .Modify(student =>
                   {
                       foreach (var sess in sessions) student.AddSession(false, sess, group);
                   }).ToList();
        await students.SaveAsync(cancellation: ct, session: session);
    }

    public async Task RemoveGroupSessionsFromStudents(IEnumerable<Student> students,
        Group group,
        CancellationToken ct = default,
        IClientSessionHandle? session = null)

    {
        foreach (var student in students)
            student.Sessions.RemoveAll(x => x.Group.ID == group.ID && x.Status == StudentSessionStatus.Upcoming);

        await students.SaveAsync(cancellation: ct, session: session);
    }

    public async Task AddNextGroupSessionToStudents(Group group,
        CancellationToken ct = default,
        IClientSessionHandle? session = null)
    {
        var students = await DB.Find<Student>(session)
                               .Match(f => f.In(x => x.ID, group.Students.Select(x => x.ID)))
                               .ExecuteAsync(ct);

        students = students
                  .Modify(student => student.AddSession(false, group?.NextSession!, group!))
                  .ToList();

        if (students.Any()) await students.SaveAsync(session, ct);
    }
}