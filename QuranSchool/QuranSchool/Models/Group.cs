using Jetsons.JetPack;
using MongoDB.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuranSchool.Models;

public class Group : ClientEntity, ICreatedOn, IModifiedOn
{
    public TeacherBase Teacher { get; set; }
    public AccountBase? CreatedBy { get; set; }
    public FormationBase Formation { get; set; }

    public List<GroupPost> Posts { get; set; } = new();
    public DateTime Start { get; set; } = DateTime.Now;
    public DateTime ExpireOn => Start.AddDays(Formation.DurationDays).Date;

    public DateTime? EndedOn
    {
        get
        {
            try
            {
                return Status == GroupStatus.Finished
                    ? Sessions.OrderByDescending(x => x.End)
                        .FirstOrDefault(x => x.Status == SessionStatus.TeacherPresent)?.Start
                    : null;
            }
            catch
            {
                return null;
            }
        }
    }

    public bool CanStartNextSession => NextSession != null
                                       && DateTime.Now >= NextSession.Start.Date
                                       && DateTime.Now <= NextSession.End;

    public string Name { get; set; }
    public List<StudentBase> Students { get; set; } = new();
    public List<Session> Sessions { get; set; } = new();

    public Session? NextSession
    {
        get
        {
            try
            {
                var next = Sessions
                    .OrderBy(x => x.Start)
                    .FirstOrDefault(x => x.Status is SessionStatus.InSession or SessionStatus.Available);
                return next;
            }
            catch
            {
                return null;
            }
        }
    }

    public Session? CurrentSession
    {
        get
        {
            try
            {
                return Sessions
                    .FirstOrDefault(x => x.Status == SessionStatus.InSession);
            }
            catch
            {
                return null;
            }
        }
    }

    public int? SessionsRemained
    {
        get
        {
            try
            {
                return Sessions?.Count(x => x.Status is SessionStatus.Available or SessionStatus.OnHold);
            }
            catch
            {
                return null;
            }
        }
    }


    public bool OnHold { get; set; }
    public bool Cancelled { get; set; } = false;
    public bool Finished { get; set; } = false;

    public bool TeacherInSession => NextSession switch
    {
        null => false,
        { Status: SessionStatus.InSession, TeacherWasPresent: true } => true,
        _ => false,
    };

    [JsonConverter(typeof(StringEnumConverter))]

    public GroupStatus Status => this switch
    {
        var group when group.Finished => GroupStatus.Finished,
        var group when group.NextSession is null || group.Cancelled => GroupStatus.Cancelled,
        var group when group.OnHold => GroupStatus.OnHold,
        var group when group.NextSession?.Status == SessionStatus.InSession => GroupStatus.InSession,
        var group when group.Sessions.All(x => x.Status == SessionStatus.Available) => GroupStatus.New,
        var group when group.Sessions.Any(x => x.Status != SessionStatus.Available) => GroupStatus.OnGoing,
        _ => GroupStatus.OnGoing,
    };

    //public GroupStatus Status
    //{
    //    get
    //    {
    //        try
    //        {
    //            return Finished
    //                ? GroupStatus.Finished
    //                : !OnHold && !Cancelled
    //                    ? NextSession?.Start <= DateTime.Now && NextSession.End >= DateTime.Now
    //                        ? GroupStatus.InSession
    //                        : Sessions.All(x => x.Status == SessionStatus.Available)
    //                            ? GroupStatus.New
    //                            : GroupStatus.OnGoing
    //                    : Cancelled
    //                        ? GroupStatus.Cancelled
    //                        : GroupStatus.OnHold;
    //        }
    //        catch
    //        {
    //            return GroupStatus.Cancelled;
    //        }
    //    }
    //}

    public DateTime? HoldDate { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }

    public void ChangeTeacher(TeacherBase teacher)
    {
        Sessions = Sessions
            .Modify(x =>
            {
                if (x.Status is SessionStatus.Available or SessionStatus.OnHold) x.Teacher = teacher;
            }).ToList();
    }

    public void CancelRemainingSessions()
    {
        Sessions = Sessions
            .Modify(x =>
            {
                if (x.Status is SessionStatus.Available or SessionStatus.OnHold) x.Cancelled = true;
            }).ToList();
    }

    public void SetOnHold(DateTime? holdGroupOn)
    {
        HoldDate = holdGroupOn?.ToDateTime().Date ?? DateTime.Today;
        OnHold = true;

        Sessions = Sessions
            .Modify(s =>
            {
                if (s.Status == SessionStatus.Available) s.OnHold = true;
            })
            .ToList();
    }

    public void ResumeSessions()
    {
        Sessions = Sessions
            .Modify(s =>
            {
                if (s.Status == SessionStatus.OnHold)
                {
                    s.OnHold = false;
                    s.Start.AddDays((HoldDate.ToDateTime() - DateTime.Today).Days);
                    s.End.AddDays((HoldDate.ToDateTime() - DateTime.Today).Days);
                }
            })
            .ToList();
    }
}