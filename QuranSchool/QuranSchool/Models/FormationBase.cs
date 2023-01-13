namespace QuranSchool.Models;

public class FormationBase : ClientEntity
{
    public string Name { get; set; }
    public int DurationDays { get; set; }
    public double Price { get; set; }
    public double PricePerHour => Hours <= 0 ? Price : Price / Hours;
    public double Hours { get; set; }
}