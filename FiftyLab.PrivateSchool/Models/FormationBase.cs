using MongoDB.Entities;

namespace FiftyLab.PrivateSchool;

public class FormationBase : Entity
{
    public string Name { get; set; }
    public int DurationDays { get; set; }
    public double Price { get; set; }
}