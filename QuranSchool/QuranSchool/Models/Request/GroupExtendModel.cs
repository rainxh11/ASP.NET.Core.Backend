using MongoDB.Bson.Serialization.Attributes;

using Newtonsoft.Json;

namespace QuranSchool.Models.Request;

public class GroupExtendModel
{
    public string Id { get; set; }
    public DateTime? Until { get; set; }
    public int? For { get; set; }
}

public class ScheduleUpdateModel : GroupExtendModel
{
    [BsonIgnore] public List<OccurrenceModel>? Occurrences { get; set; } = new();
    [BsonIgnoreIfNull] [JsonIgnore] public List<SessionModel>? SessionModels { get; set; } = new();
}