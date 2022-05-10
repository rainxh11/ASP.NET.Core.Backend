using FiftyLab.PrivateSchool.Models;
using MongoDB.Entities;

namespace FiftyLab.PrivateSchool;

public class Student : StudentBase, ICreatedOn, IModifiedOn
{
    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public DateTime ModifiedOn { get; set; }
    public List<Parent> Parents { get; set; }
    public string Description { get; set; }
    public Avatar Avatar { get; set; }
    public string Address { get; set; }
    public string PhoneNumber { get; set; }
    public AccountBase CreatedBy { get; set; }

    public StudentBase ToBase()
    {
        return new StudentBase()
        {
            DateOfBirth = DateOfBirth,
            Gender = Gender,
            ID = ID,
            Name = Name
        };
    }
}