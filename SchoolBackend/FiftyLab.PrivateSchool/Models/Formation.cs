using MongoDB.Entities;

namespace FiftyLab.PrivateSchool;

public class Formation : FormationBase, ICreatedOn, IModifiedOn
{
    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public DateTime ModifiedOn { get; set; }
    public bool Enabled { get; set; } = true;
    public AccountBase CreatedBy { get; set; }
    public FormationBase ToBase()
    {
        return new FormationBase()
        {
            Name = Name,
            DurationDays = DurationDays,
            Price = Price,
        };
    }
}