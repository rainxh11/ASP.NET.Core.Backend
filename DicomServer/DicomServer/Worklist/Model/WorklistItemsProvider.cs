using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Entities;
using Akavache;
using System.Reactive.Linq;
using System.Linq;
using DicomServer;
using DicomServer.Models;
using DicomServer.Helpers;

namespace DicomServerWorkList.Model
{
    public class WorklistItemsProvider : IWorklistItemsSource
    {

        public List<WorklistItem> GetAllCurrentWorklistItems()
        {
            //return BlobCache.LocalMachine.GetAllObjects<WorklistItem>().GetAwaiter().GetResult().ToList();
            return Run.Sync(() => StudyToWorklist.GetWorklist());

        }

    }
}
