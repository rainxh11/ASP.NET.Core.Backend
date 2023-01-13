using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive;
using Refit;
using OrthancCrawler.Models;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Threading;
using System.Net.Http;

namespace OrthancCrawler
{
    public class Worker
    {
        static IOrthancApi api;
        static IOrthancApi orthanc;
        public static async Task StartAsync()
        {

            var config = Config.GetConfig();
            api = RestService.For<IOrthancApi>(new HttpClient()
            {
                BaseAddress = new Uri(config.OrthancUrl),
                Timeout = TimeSpan.FromSeconds(90),
                MaxResponseContentBufferSize = 8192 * 1024
            });
            orthanc = RestService.For<IOrthancApi>(new HttpClient()
            {
                BaseAddress = new Uri(config.OrthancPostgresUrl),
                Timeout = TimeSpan.FromMinutes(90),
                MaxResponseContentBufferSize = 8192 * 1024
            });

            var folder = new DirectoryInfo(config.DestinationFolder);
            if (!folder.Exists) folder.Create();

            var doable = Observable
                .Interval(TimeSpan.FromSeconds(90))
                .Repeat()
                .ObserveOn(TaskPoolScheduler.Default)
                .Select(x =>
                {
                    try
                    {
                        return Observable.FromAsync(() => orthanc.GetStudies());
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Merge(1)
                .SelectMany(x => folder.GetFiles("*.zip").Where(file => !x.Contains(file.Name.Replace(".zip", ""))))
                .Delay(TimeSpan.FromSeconds(10))
                .Do(async file => await UploadFile(file));

            await Task.Run(async () =>
            {
                while (true)
                {
                    var studies = await api.GetStudies();
                    var existingStudies = await orthanc.GetStudies();

                    var newStudies = studies.Select(x => x).Except(existingStudies);

                    await Parallel.ForEachAsync(newStudies, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, async (study, ct) =>
                    {
                        try
                        {
                            await DownloadAndUploadBack(study);
                        }
                        catch
                        {

                        }

                    });

                    /*foreach (var study in newStudies)
                    {
                        try
                        {
                            await DownloadAndUploadBack(study);
                        }
                        catch
                        {

                        }
                    }*/
                    await Task.Delay(5000);
                }
            });

            /*while (true)
            {
                foreach(var study in studies)
                {
                    await DownloadAndSave(study.Id);

                }
                await Task.Delay(5000);
            }*/



        }
        public static void Start()
        {
            /*var config = Config.GetConfig();
            api = RestService.For<IDicomServerApi>(config.Url);

            var folder = new DirectoryInfo(config.DestinationFolder);
            if (!folder.Exists) folder.Create();


            Observable
                .Interval(TimeSpan.FromSeconds(30))
                .Repeat()
                .Select(x =>
                {
                    try
                    {
                        return Observable.FromAsync(() => api.GetStudies());
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Merge(10)
                .SelectMany(x => x)
                .DistinctUntilChanged()
                .Do(async x => await api.QueueArchiveJob(x.Id));
                //.Subscribe();

            Observable
               .Interval(TimeSpan.FromSeconds(5))
               .Repeat()
               .Select(x => Observable.FromAsync(() => api.GetJobs()))
               .Merge(1)
               .SelectMany(x => x.Where(j => j.Job.IsFinished()))
               .Buffer(4)
               .SelectMany(x => x)
               .Where(x => x != null)
               .Where(x => x.Job.IsFinished())
               .DistinctUntilChanged(x => x.Job.ID)
               .ObserveOn(TaskPoolScheduler.Default)
               .Do(async x => await DownloadAndSave(x))
               .SubscribeOn(TaskPoolScheduler.Default);
                //.Subscribe();*/


        }
        public static async Task TryDownloadStream(string name)
        {
            try
            {
                var config = Config.GetConfig();
                HttpClient client = new HttpClient();
                var url = $"http://127.0.0.1:90/{name}";

                HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                Stream streamToReadFrom = await response.Content.ReadAsStreamAsync();

                using (FileStream outputFileStream = new FileStream($@"E:\PACS_TEMP\{name}.bin", FileMode.Create))
                {

                    await streamToReadFrom.CopyToAsync(outputFileStream, 1024 * 1024);

                    outputFileStream.Close();
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public static async Task UploadFile(FileInfo file)
        {
            try
            {
                using (FileStream fileStream = new FileStream(file.FullName, FileMode.Open))
                {
                    Console.WriteLine($"Uploading File: {file.FullName}");

                    await orthanc.UploadInstance(new StreamPart(fileStream, file.Name, "application/octet-stream"));
                    fileStream.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public static async Task DownloadAndUploadBack(string id)
        {
            try
            {
                var config = Config.GetConfig();
                var fileName = $"{id}.zip";

                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(15);

                var url = $"http://172.16.1.198:8042/studies/{id}/archive";

                HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                Stream streamToReadFrom = await response.Content.ReadAsStreamAsync();

                Console.WriteLine($"Downloading & Uploading Back : {fileName}");

                await orthanc.UploadInstance(new StreamPart(streamToReadFrom, fileName, "application/octet-stream"));

                streamToReadFrom.Close();
            }
            catch (Exception ex)
            {

                Console.WriteLine(ex.Message);
            }
        }
        public static async Task DownloadAndSave(string id)
        {
            var config = Config.GetConfig();
            var fileName = Path.Combine(config.DestinationFolder, $"{id}.unfinished");
            var saveFile = new FileInfo(fileName);
            var destination = new FileInfo(fileName.Replace("unfinished", "zip"));

            try
            {
                var file = new FileInfo(fileName.Replace("unfinished", "zip"));
                if (file.Exists)
                {
                    if (file.Length > 100) return;

                }

                Console.WriteLine($"Downloading Study: {id}");

                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(15);

                var url = $"http://172.16.1.198:8042/studies/{id}/archive";

                HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                Stream streamToReadFrom = await response.Content.ReadAsStreamAsync();


                using (FileStream outputFileStream = new FileStream(fileName, FileMode.Create))
                {
                    Console.WriteLine($"Writing File: {fileName}");

                    await streamToReadFrom.CopyToAsync(outputFileStream, 8192 * 2048);

                    outputFileStream.Close();
                    if (saveFile.Exists)
                    {
                        saveFile.MoveTo(destination.FullName, true);
                    }
                }
            }
            catch(Exception ex)
            {
                if (saveFile.Exists)
                {
                    saveFile.Delete();
                }
                Console.WriteLine(ex.Message);
            }
        }
    }
}
