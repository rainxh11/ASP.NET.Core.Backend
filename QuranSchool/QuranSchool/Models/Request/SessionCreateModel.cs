using MongoDB.Entities;

namespace QuranSchool.Models.Request;

public class SessionCreateModel : SessionModel
{
    public One<Teacher>? Teacher { get; set; }
}