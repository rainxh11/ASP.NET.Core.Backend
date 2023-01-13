using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace QuranSchool;

public static class Extensions
{
    public static T RunSync<T>(this Task<T> task, bool continueOnCapturedContext = false)
    {
        return task
            .ConfigureAwait(continueOnCapturedContext)
            .GetAwaiter()
            .GetResult();
    }

    public static void RunSync(this Task task, bool continueOnCapturedContext = false)
    {
        task
            .ConfigureAwait(continueOnCapturedContext)
            .GetAwaiter()
            .GetResult();
    }

    public static async Task<T> MapAsync<T>(this Task<T> task, Func<T, Task<T>> function)
    {
        var value = await task;
        return await function(value);
    }

    public static async Task<TResult> MapAsync<T, TResult>(this Task<T> task, Func<T, TResult> function)
    {
        var value = await task;
        return function(value);
    }

    public static async Task DoAsync<T>(this Task<T> task, Action<T> action)
        => action(await task);

    public static async Task DoAsync<T>(this Task<T> task, Func<T, Task> action)
        => await action(await task);

}

public static class BsonExtensions
{
    public static object DeserializeAsObject(this BsonDocument document)
    {
        return BsonSerializer.Deserialize<object>(document);
    }

    public static T Deserialize<T>(this BsonDocument document)
    {
        return BsonSerializer.Deserialize<T>(document);
    }

    public static object DeserializeAsObject(this BsonValue bsonValue)
    {
        return BsonSerializer.Deserialize<object>(bsonValue.AsBsonDocument);
    }

    public static T Deserialize<T>(this BsonValue bsonValue)
    {
        return BsonSerializer.Deserialize<T>(bsonValue.AsBsonDocument);
    }
}

public static class CollectionExtensions
{

    public static string StringJoin<T>(this IEnumerable<T> enumerable, string separator)
        => string.Join(separator, enumerable);
    public static IEnumerable<TResult> DestinctSelect<T, TResult>(this IEnumerable<T> enumerable, Func<T, TResult> selector)
        => enumerable
            .Distinct()
            .Select(selector);

    public static IEnumerable<TResult> DestinctBySelect<T, TResult, TDistinctor>(this IEnumerable<T> enumerable,
        Func<T, TDistinctor> distinctor, Func<T, TResult> selector)
    => enumerable
        .DistinctBy(distinctor)
        .Select(selector);

    public static IEnumerable<T> Modify<T>(this IEnumerable<T> collection, Action<T> modify)
    {
        return collection.Select(x =>
        {
            modify(x);
            return x;
        });
    }

    public static TOutput PipelineWith<T, TInput, TOutput>(this IEnumerable<T> enumerable,
        Func<T, TInput> stageFunction,
        Func<TInput, TOutput> lastStage)
    {
        var input = default(TInput);
        foreach (var item in enumerable) input = stageFunction(item);

        return lastStage(input!);
    }

    public static TInput PipelineWith<T, TInput>(this IEnumerable<T> enumerable,
    Func<T, TInput> stageFunction)
    {
        var input = default(TInput);
        foreach (var item in enumerable) input = stageFunction(item);

        return input!;
    }

    public static TResult Map<T, TResult>(this IEnumerable<T> enumerable, Func<IEnumerable<T>, TResult> mapper)
        => mapper(enumerable);
}


public static class ObjectExtensions
{
    public static TResult Map<T, TResult>(this T input, Func<T, TResult> mapper)
        => mapper(input);
}

public static class DateTimeExtensions
{
    public static string ToCronExpression(this IEnumerable<DateTime> times)
        => times
            .Distinct()
            .Map(t => new
            {
                Seconds = t.DestinctBySelect(x => x.ToString("ss"), x => x.Second).StringJoin(","),
                Minutes = t.DestinctBySelect(x => x.ToString("mm"), x => x.Minute).StringJoin(","),
                Hours = t.DestinctBySelect(x => x.ToString("HH"), x => x.Hour).StringJoin(","),
                DayOfMonth = t.DestinctBySelect(x => x.ToString("d"), x => x.Day).StringJoin(","),
                Month = t.DestinctBySelect(x => x.Month, x => x.Month).StringJoin(","),
            })
            .Map(t => $"{t.Seconds} {t.Minutes} {t.Hours} {t.DayOfMonth} {t.Month} ?");
}
