using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocumentWatcher.Models;
using System.IO;
using System.Diagnostics;
using MongoDB.Driver;

namespace DocumentWatcher.Helpers
{
    public static class MongoHelper
    {
        public static async Task Init()
        {
            try
            {
                var config = ConfigHelper.GetConfig();

                await DB.InitAsync(config.MongoDBDatabase, MongoClientSettings.FromConnectionString(config.MongoDBConnectionString));

                MongoHelper.StartWatchers();
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.Message);
                //Console.WriteLine("Press Any Key to Exit.");
                //Console.ReadKey();
                Process.GetCurrentProcess().Kill();
            }
        }

        public static Watcher<RisStudy> RisStudyWatcher_OnCreate;
        public static Watcher<RisStudy> RisStudyWatcher_OnUpdate;

        public static void StartWatchers()
        {
            RisStudyWatcher_OnCreate = DB.Watcher<RisStudy>("studies_new");
            RisStudyWatcher_OnUpdate = DB.Watcher<RisStudy>("studies_update");
            try
            {
                RisStudyWatcher_OnCreate.Start(EventType.Created);
                RisStudyWatcher_OnUpdate.Start(EventType.Updated);
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.Message);
            }
        }

        private static List<StudyFile> _studies = new List<StudyFile>();

        public static List<StudyFile> GetStudies()
        {
            return _studies.OrderByDescending(x => x.Study.createdAt).ToList();
        }

        public static async Task GetAllStudies()
        {
            try
            {
                var config = ConfigHelper.GetConfig();
                var ids = new DirectoryInfo(config.DocumentFolder)
                    .GetFiles("*.docx")
                    .ToDictionary(file => file, file => Convert.ToInt32(file.Name.Split("_")[0]));

                var studies = new List<RisStudy>();
                foreach (var id in ids)
                {
                    var study = await DB.Find<RisStudy>().Match(x => x._id == id.Value).ExecuteFirstAsync();
                    studies.Add(study);
                }

                _studies = new List<StudyFile>();

                foreach (var study in studies)
                {
                    var client = await DB.Find<RisClient>()
                        .MatchID(study.client.ToString())
                        .ExecuteSingleAsync();

                    _studies.Add(new StudyFile()
                    {
                        Client = client,
                        Study = study,
                        File = ids.Where(x => x.Value == study._id).First().Key
                    });
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.Message);
            }
        }

        public static void WatchStudies()
        {
            RisStudyWatcher_OnCreate.OnChanges += RisStudyWatcher_OnCreate_OnChanges;
        }

        private static void RisStudyWatcher_OnCreate_OnChanges(IEnumerable<RisStudy> obj)
        {
            var config = ConfigHelper.GetConfig();
            var folder = new DirectoryInfo(config.DocumentFolder);
            try
            {
                if (!folder.Exists)
                {
                    folder.Create();
                }
            }
            catch
            {
            }

            foreach (var study in obj)
            {
                try
                {
                    DocumentHelper.CreateNewStudyFile(study);
                }
                catch
                {
                }
            }
        }
    }
}