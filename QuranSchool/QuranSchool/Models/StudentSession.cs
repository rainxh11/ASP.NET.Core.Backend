using MongoDB.Bson;
using MongoDB.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuranSchool.Models;

public class StudentSession
{
    public StudentSession(string id, bool wasPresent, Session session, Group group, FormationBase formation)
    {
        Start = session.Start;
        End = session.End;
        WasPresent = wasPresent;
        Group = group;
        Formation = formation;
        ID = id;
    }

    public string ID { get; set; } = ObjectId.GenerateNewId().ToString();

    public DateTime? WasPresentOn { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool WasPresent { get; set; }
    public One<Group> Group { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public StudentSessionStatus Status => Start > DateTime.Now ? StudentSessionStatus.Upcoming :
        WasPresent ? StudentSessionStatus.Present : StudentSessionStatus.Absent;

    public FormationBase Formation { get; set; }
    public double Price => Formation.PricePerHour * (End - Start).TotalHours;

    public void TogglePresence(bool toggle = true)
    {
        WasPresentOn = DateTime.Now;
        WasPresent = toggle;
    }
}