using DicomServer.Models;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DicomServer.Helpers
{
    public static class MongoHelper
    {
        public static Watcher<RisStudy> RisStudyWatcher_OnCreate;
        public static Watcher<RisStudy> RisStudyWatcher_OnUpdate;

        public static string GetAccessionNumber(RisClient client)
        {
            var number = client.NextSequentialNumberAsync();
            return $"CIMESPOIR_PATIENT-{number.ToString().PadLeft(10, '0')}";

        }
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
                Console.WriteLine(ex.Message);
            }
        }
    }
}
