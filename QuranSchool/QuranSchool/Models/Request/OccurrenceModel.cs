using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using QuranSchool.Models.Validations;

namespace QuranSchool.Models.Request;

public class OccurrenceModelEqualityComparer : IEqualityComparer<OccurrenceModel>
{
    public bool Equals(OccurrenceModel x, OccurrenceModel y)
    {
        return DateOverlap.IsOverlapped(x.GetTimes().start, x.GetTimes().end, y.GetTimes().start, y.GetTimes().end) &&
               x.Day == y.Day;
    }

    public int GetHashCode(OccurrenceModel obj)
    {
        return Invio.Hashing.HashCode.From(obj.StartTime, obj.EndTime, obj.Day);
    }
}

public class OccurrenceModel
{
    public string StartTime { get; set; }
    public string EndTime { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public DayOfWeek Day { get; set; }

    public (TimeOnly start, TimeOnly end) GetTimes()
    {
        return (TimeOnly.FromTimeSpan(TimeSpan.Parse(StartTime)),
            TimeOnly.FromTimeSpan(TimeSpan.Parse(EndTime)));
    }
}