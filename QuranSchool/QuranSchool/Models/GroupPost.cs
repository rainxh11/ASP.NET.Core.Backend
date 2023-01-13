using MongoDB.Bson;

namespace QuranSchool.Models;

public class GroupPost
{
    public string ID { get; set; } = ObjectId.GenerateNewId().ToString();
    public AccountBase CreatedBy { get; set; }
    public string Content { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.Now;
}