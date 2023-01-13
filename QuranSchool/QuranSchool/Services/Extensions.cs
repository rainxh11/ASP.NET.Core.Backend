using MongoDB.Driver;
using MongoDB.Entities;
using System.Reactive.Linq;

namespace QuranSchool.Services;

public static class Extensions
{
    public static IObservable<ChangeStreamDocument<T>> ToObservableChangeStream<T>(this Watcher<T> watcher)
        where T : IEntity
    {
        return Observable
            .FromEvent<IEnumerable<ChangeStreamDocument<T>>>(
                x => watcher.OnChangesCSD += x,
                x => watcher.OnChangesCSD -= x)
            .SelectMany(x => x);
    }
}