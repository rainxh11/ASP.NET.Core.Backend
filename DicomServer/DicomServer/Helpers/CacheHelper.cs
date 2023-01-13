using DicomServer.Helper;
using DicomServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Refit;
using Akavache;
using System.Reactive.Linq;
using MoreLinq.Extensions;
using System.Globalization;
using System.Reactive.Threading.Tasks;
using Newtonsoft.Json.Serialization;

namespace DicomServer.Helpers
{
    public class CacheHelper
    {
        public static IOrthancApi orthancApi;
        public static void Init()
        {
            var config = ConfigHelper.GetConfig();
            orthancApi = RestService.For<IOrthancApi>(config.OrthancApi.Host, new RefitSettings(new NewtonsoftJsonContentSerializer()));

            Observable
                .Interval(TimeSpan.FromMinutes(1))
                .Repeat()
                .Do(x => BlobCache.LocalMachine.Vacuum())
                .Subscribe(x =>
                {
                    Console.WriteLine($"[Cache] Cache Cleanup Task Started.");
                });
        }
        public static async Task RemoveStudyFromImageCache(string id)
        {
            try
            {
                await Task.WhenAny(
                    BlobCache.LocalMachine.InvalidateObject<byte[]>($"{id}_webp").ToTask(),
                    BlobCache.LocalMachine.InvalidateObject<byte[]>($"{id}").ToTask()
                    );
            }
            catch
            {

            }
        }
        public static async Task RemoveStudyFromCache(string id)
        {
            try
            {
                await BlobCache.LocalMachine.InvalidateObject<RisImagingStudy>(id);
            }
            catch
            {

            }
        }
        public static async Task<IEnumerable<RisImagingStudy>> GetStudies()
        {
            var config = ConfigHelper.GetConfig();
            var orthancStudies = await orthancApi.GetStudies();

            if (config.StudiesCache.Enabled)
            {
                try
                {
                    var cachedStudies = await BlobCache.LocalMachine.GetAllObjects<RisImagingStudy>();

                    orthancStudies = orthancStudies.Except(cachedStudies.Select(x => x.Id)).ToList();

                }
                catch
                {

                }            
            }   

            var studies = orthancStudies./*AsParallel().WithExecutionMode(ParallelExecutionMode.ForceParallelism).*/Select(id =>
            {
                var study = orthancApi.GetStudy(id).GetAwaiter().GetResult();
                var instances = orthancApi.GetStudyInstances(id).GetAwaiter().GetResult().Take(5);
                    var result = new RisImagingStudy()
                {
                    Id = id,
                    Description = study.MainDicomTags.StudyDescription?.Trim().ToUpper() ?? string.Empty,
                    StudyDate = new Func<DateTime>(() =>
                    {
                        try
                        {
                            return DateTime.ParseExact(study.MainDicomTags.StudyDate + " " + study.MainDicomTags.StudyTime.Split('.')[0], "yyyyMMdd HHmmss", CultureInfo.InvariantCulture);
                        }
                        catch
                        {
                            return new DateTime();
                        }
                    })(),
                    Instances = new Func<List<string>>(() =>
                    {
                        try
                        {
                            return instances.Select(x => x.ID).ToList();
                        }
                        catch
                        {
                            return Enumerable.Empty<string>().ToList();
                        }
                    })(),
                    PatientId = study.PatientMainDicomTags.PatientID,
                    PatientName = study.PatientMainDicomTags.PatientName.Replace("^", " ").Trim().ToUpper(),
                    StudyInstanceUID = study.MainDicomTags.StudyInstanceUID
                };
                try
                {
                    BlobCache.LocalMachine.InsertObject<RisImagingStudy>(id, result, TimeSpan.FromDays(config.StudiesCache.ExpirationDays));
                }
                catch
                {

                }

                return result;
                
            }).Where(x => x != null).OrderByDescending(x => x.StudyDate).ToList();


            try
            {
                if (studies == null) studies = new List<RisImagingStudy>();

                var cachedStudies = await BlobCache.LocalMachine.GetAllObjects<RisImagingStudy>();

                studies = cachedStudies.UnionBy(studies, x => x.Id).ToList();

            }
            catch
            {

            }

            return studies;
        }
        public static async Task<byte[]> DownloadFile(string id)
        {
            var config = ConfigHelper.GetConfig();
            return await BlobCache.LocalMachine.DownloadUrl($"{config.OrthancApi.Host}/studies/{id}/archive", TimeSpan.FromHours(config.ArchiveCache.ExpirationHours)).ToTask();
        }
    }
}
