using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Entities;

namespace QuranSchool.Models;

public class Student : StudentBase, ICreatedOn, IModifiedOn
{
    public string Description { get; set; }
    public Avatar? Avatar { get; set; }
    public string Address { get; set; }
    public AccountBase CreatedBy { get; set; }

    public string PlaceOfBirth { get; set; }
    public string StudyLevel { get; set; }
    public List<StudentSession> Sessions { get; set; } = new();

    [BsonIgnore] public double TotalDebt { get; set; } = 0;
    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public DateTime ModifiedOn { get; set; }

    public void AddSession(bool wasPresent, Session session, Group group)
    {
        if (Sessions.Any(x => x.ID == session.ID))
            ToggleSessionPresence(session.ID, wasPresent);
        else
            Sessions.Add(new StudentSession(session.ID, wasPresent, session, group, group.Formation));

        Sessions = Sessions.DistinctBy(x => x.ID).ToList();
    }

    public void ToggleSessionPresence(string id, bool presence)
    {
        if (Sessions.Any(x => x.ID == id))
            Sessions = Sessions.Select(x =>
            {
                if (x.ID == id)
                {
                    x.WasPresent = presence;
                    x.WasPresentOn = DateTime.Now;
                }

                return x;
            }).ToList();
    }

    public StudentBase ToBase()
    {
        return new StudentBase
        {
            PhoneNumber = PhoneNumber,
            Parents = Parents,
            DateOfBirth = DateOfBirth,
            Gender = Gender,
            ID = ID,
            Name = Name
        };
    }
}