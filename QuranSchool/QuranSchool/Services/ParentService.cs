using MongoDB.Driver;
using MongoDB.Entities;
using QuranSchool.Models;

namespace QuranSchool.Services;

public class ParentService
{
    public async Task<List<Student>> GetParentStudents(string id, CancellationToken ct)
    {
        var students = await DB.Find<Student>()
            .Match(f => f.ElemMatch(x => x.Parents,
                new FilterDefinitionBuilder<Parent>().Eq(p => p.ID, id)))
            .ExecuteAsync(ct);

        return students;
    }
}