using Hangfire;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Entities;
using QuranSchool.Models;
namespace QuranSchool.Services;

public class SessionEndCollector
{ 
    public async Task<List<DateTime>> GetAvaillableSessionsEndTimes(CancellationToken ct = default)

	{
		var sessions = await DB.Fluent<Group>()
			.Match(f => f.Ne(x => x.Sessions, null))
			.Match(f => f.ElemMatch(x => x.Sessions,
				new FilterDefinitionBuilder<Session>().Gte(x => x.End, new BsonDateTime(DateTime.Now)) &
				new FilterDefinitionBuilder<Session>().Eq(x => x.Cancelled, false) &
				new FilterDefinitionBuilder<Session>().Eq(x => x.OnHold, false)))
			.Unwind(x => x.Sessions)
			.ReplaceRoot(x => x["Sessions"])
			.Match(new BsonDocument("$expr", new BsonDocument("$and", new BsonArray
			{
				new BsonDocument("$gte", new BsonArray { "$End", new BsonDateTime(DateTime.Now) }),
				new BsonDocument("$eq", new BsonArray { "$Cancelled", false }),
				new BsonDocument("$eq", new BsonArray { "$OnHold", false })
			})))
			.ToListAsync(ct)
			.MapAsync(times => times.Select(x => x["End"].AsDateTime)
				.Distinct()
				.OrderBy(x => x)
				.ToList()
			);

		return sessions;
	}
}