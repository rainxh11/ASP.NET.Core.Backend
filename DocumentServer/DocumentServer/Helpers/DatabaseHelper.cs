using MongoDB.Driver;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RisReport.Library.Models;
using RisReport.Library;

namespace RisDocumentServer.Helpers
{
    public class DatabaseHelper
    {
        public static Watcher<RisClient> RisClientWatcher;

        public static void StartWatchers()
        {
            RisClientWatcher = DB.Watcher<RisClient>("clients");
            try
            {
                RisClientWatcher.Start(EventType.Created);
                RisClientWatcher.OnChangesAsync += RisClientWatcher_OnChangesAsync;
            }
            catch (Exception ex)
            {
                Program.Logger.Error(ex.Message);
                Console.WriteLine(ex.Message);
            }
        }

        private static async Task RisClientWatcher_OnChangesAsync(IEnumerable<RisClient> args)
        {
            await Helpers.RisClientsDeduplicator.CleanClientNames();
            //await Helpers.RisClientsDeduplicator.Deduplicate();
        }

        public async static Task InitDatabase()
        {
            try
            {
                var config = Models.ConfigModel.GetConfig();
                await DB.InitAsync(config.MongoDBDatabase, MongoClientSettings.FromConnectionString(config.MongoDBConnectionString));
            }
            catch (Exception ex)
            {
                Program.Logger.Error(ex.Message);
                Console.WriteLine(ex.Message);
            }
        }
    }
}