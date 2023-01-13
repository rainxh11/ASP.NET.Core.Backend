using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using QuranSchool.Models.Validations;

namespace QuranSchool.Models;

public class Session : IEquatable<Session>
{
    public string ID { get; set; } = ObjectId.GenerateNewId().ToString();


    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool Cancelled { get; set; } = false;
    public bool OnHold { get; set; } = false;
    public FormationBase Formation { get; set; }
    public TeacherBase Teacher { get; set; }
    public int Version { get; set; } = 0;

    [BsonIgnore] public double Price => Formation.PricePerHour * (End - Start).TotalHours;
    public bool TeacherWasPresent { get; set; } = false;

    [JsonConverter(typeof(StringEnumConverter))]
    public SessionStatus Status => this switch
    {
        var session when session.Cancelled => SessionStatus.Cancelled,
        var session when session.OnHold => SessionStatus.OnHold,

        var session when session.End < DateTime.Now => session.TeacherWasPresent
            ? SessionStatus.TeacherPresent
            : SessionStatus.TeacherAbsent,
        var session when session.Start <= DateTime.Now && session.End >= DateTime.Now => SessionStatus.InSession,
        var session when session.Start.Date == DateTime.Now.Date && session.TeacherWasPresent =>
            SessionStatus.InSession,


        _ => SessionStatus.Available
    };

    public bool Equals(Session? other)
    {
        return DateOverlap.IsOverlapped(Start, End, other.Start, other.End);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Session)obj);
    }

    public override int GetHashCode()
    {
        return Invio.Hashing.HashCode.From(Start, End);
    }
}

public class SessionEqualityComparer : IEqualityComparer<Session>
{
    public bool Equals(Session x,
        Session y)
    {
        return DateOverlap.IsOverlapped(x.Start, x.End, y.Start, y.End);
    }

    public int GetHashCode(Session obj)
    {
        return Invio.Hashing.HashCode.From(obj.Start, obj.End);
    }
}