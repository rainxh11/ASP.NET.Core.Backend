using MongoDB.Entities;

namespace QuranSchool.Models;

public class Formation : FormationBase, ICreatedOn, IModifiedOn
{
    public bool Enabled { get; set; } = true;
    public AccountBase CreatedBy { get; set; } = new();
    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public DateTime ModifiedOn { get; set; }

    public FormationBase ToBase()
    {
        return new FormationBase
        {
            ID = ID,
            Name = Name,
            DurationDays = DurationDays,
            Price = Price
        };
    }
}