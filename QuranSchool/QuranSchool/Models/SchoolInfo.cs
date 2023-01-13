using MongoDB.Entities;

namespace QuranSchool.Models;

[Collection("School")]
public class SchoolInfo : ClientEntity
{
    public string Name { get; set; }
    public string Address { get; set; }
    public string Website { get; set; }
    public string PhoneNumber { get; set; }
}