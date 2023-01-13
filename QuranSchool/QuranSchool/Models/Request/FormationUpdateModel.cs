using MongoDB.Bson.Serialization.Attributes;

namespace QuranSchool.Models.Request;

public class FormationUpdateModel : FormationModel
{
    [BsonIgnoreIfNull] public bool? Enabled { get; set; }
}