using System;
using System.Linq;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using Refit;
using DicomServer.Models;
using DicomServer.Helper;
using System.Diagnostics.CodeAnalysis;
using MoreLinq.Extensions;
using Invio.Hashing;
using System.Threading.Tasks;
using WebSocketSharp.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Hangfire;
using Hangfire.MemoryStorage;

namespace DicomServer.Helpers
{
    public class RefreshHelper
    {
        public class RisWebSocket : WebSocketBehavior
        {
        }
        public class OrthancStudiesIdListEqualityComparer : IEqualityComparer<List<string>>
        {
            public bool Equals(List<string> x, List<string> y)
            {
                return x.Except(y, StringComparer.OrdinalIgnoreCase).Count() == 0;
            }

            public int GetHashCode([DisallowNull] List<string> obj)
            {
                unchecked
                {
                    return Invio.Hashing.HashCode.From(obj);
                }
            }
        }
        public static void Start()
        {
            var config = ConfigHelper.GetConfig();
            var orthancApi = RestService.For<IOrthancApi>(config.OrthancApi.Host, new RefitSettings(new NewtonsoftJsonContentSerializer()));

            var wserver = new WebSocketServer(config.WebSocketServerUrl);
            wserver.AddWebSocketService<RisWebSocket>("/dicomserverwebsocket/studies");
            wserver.Start();

            Observable
                .Interval(TimeSpan.FromSeconds(config.RefreshOptions.JobsRefreshSeconds))
                .Repeat()
                .Select(x => Observable.FromAsync(async () =>
               {
                   try
                   {
                       return await OrthancHelper.GetJobs(orthancApi);
                   }
                   catch (Exception ex)
                   {
                       Console.WriteLine(ex.Message);
                       return null;
                   }
               }))
                .Merge(1)
                .Where(x => x != null)
                .DistinctUntilChanged(new ResponseJobsEqualityComparer())
                .Do(x =>
                {
                    try
                    {
                        wserver.WebSocketServices.Broadcast("jobs_changed");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                })
                .Subscribe();

            Observable
                .Interval(TimeSpan.FromSeconds(config.RefreshOptions.StorageRefreshSeconds))
                .Repeat()
                .Select(x => Observable.FromAsync(async () =>
                {
                    try
                    {
                        return await PACSStatusHelper.GetStorageStatus();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return null;
                    }
                }))
                .Merge(1)
                .Where(x => x != null)
                .DistinctUntilChanged()
                .Do(x =>
                {
                    try
                    {
                        wserver.WebSocketServices.Broadcast("storage_changed");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                })
                .Subscribe(x =>
                {
                    try
                    {
                        Console.WriteLine("[WebSocket] Storage Changed.");
                        Console.WriteLine(JsonConvert.SerializeObject(x, Formatting.Indented));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                });

            //GlobalConfiguration.Configuration.UseMemoryStorage();

            Observable
                .Interval(TimeSpan.FromSeconds(config.RefreshOptions.PACSStudiesRefreshSeconds))
                .Repeat()
                .Select(x => Observable.FromAsync(async () =>
                {
                    try
                    {
                        return await orthancApi.GetStudies();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return null;
                    }
                }))
                .Merge(1)
                .Where(x => x != null)
                .DistinctUntilChanged(new OrthancStudiesIdListEqualityComparer())
                .Do(x => Task.Run(async () => await CacheHelper.GetStudies()))
                .Do( x =>
                {
                    try
                    {
                        wserver.WebSocketServices.Broadcast("studies_changed");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                })
                .Subscribe(x =>
                {
                    try
                    {
                        Console.WriteLine($"[WebSocket] ({x.Count}) Studies Refreshed.");
                    }
                    catch
                    {

                    }
                });
        }
    }
}
