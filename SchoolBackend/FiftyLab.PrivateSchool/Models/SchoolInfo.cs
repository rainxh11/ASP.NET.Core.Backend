using MongoDB.Entities;

namespace FiftyLab.PrivateSchool.Models;

public class SchoolInfo : Entity
{
    public string Name { get; set; }
    public string Address { get; set; }
    public string Website { get; set; }
    public string PhoneNumber { get; set; }
}