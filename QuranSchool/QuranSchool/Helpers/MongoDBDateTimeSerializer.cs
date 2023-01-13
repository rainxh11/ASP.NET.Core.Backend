using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace QuranSchool.Helpers;

public class MongoDBDateTimeSerializer : DateTimeSerializer
{
    //  MongoDB returns datetime as DateTimeKind.Utc, which can't be used in our timezone conversion logic
    //  We overwrite it to be DateTimeKind.Unspecified
    public override DateTime Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var obj = base.Deserialize(context, args);
        var result = new DateTime(obj.Ticks, DateTimeKind.Local);
        return result;
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTime value)
    {
        var utcValue = new DateTime(value.Ticks, DateTimeKind.Utc);
        base.Serialize(context, args, utcValue);
    }
}