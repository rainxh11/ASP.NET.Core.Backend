// Copyright (c) 2012-2020 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using FellowOakDicom;
using FellowOakDicom.Network;
using System;
using System.Collections.Generic;
using System.Threading;
using DicomServerWorkList.Model;
using DicomServerWorkList;
using DicomServer.Helpers;
using System.Linq;
using DicomServer;

namespace DicomServerWorkList
{
   public class WorklistServer
   {

      private static IDicomServer? _server;
      private static Timer? _itemsLoaderTimer;


      protected WorklistServer()
      {
      }

      public static string? AETitle { get; set; }


      public static IWorklistItemsSource CreateItemsSourceService => new WorklistItemsProvider();

      public static List<WorklistItem> CurrentWorklistItems { get; private set; }

      public static void Start(int port, string aet)
      {
         AETitle = aet;
         _server = DicomServerFactory.Create<WorklistService>(port);
            // every 30 seconds the worklist source is queried and the current list of items is cached in _currentWorklistItems
            CurrentWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItems();
            MongoHelper.RisStudyWatcher_OnCreate.OnChanges += RisStudyWatcher_OnCreate;

         /*_itemsLoaderTimer = new Timer((state) =>
         {
            var newWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItems();
            CurrentWorklistItems = newWorklistItems;
         }, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));*/
      }

        private static  void RisStudyWatcher_OnCreate(IEnumerable<DicomServer.Models.RisStudy> args)
        {
            /*
            var workList =  args.Select(x => 
            {
                var client = Run.Sync(() => x.GetClient());
                return new WorklistItem()
                {
                    AccessionNumber = client.GetAccession(),
                    DateOfBirth = client.birthdate,
                    ExamDateAndTime = x.createdAt,
                    ExamDescription = x.GetDescription(),
                    PatientID = client.ID,
                    StudyID = x.ID,
                    StudyUID = x.ID,
                    PerformingPhysician = "DR LAGHOUATI",
                    PatientName = client.Name,
                    Sex = client.gender.Substring(0, 1).ToUpper(),
                    ReferringPhysician = x.doctor,
                    ProcedureStepID = x.ID
                };

            }).ToList();
            CurrentWorklistItems.AddRange(workList.ToList());
            */
            CurrentWorklistItems = CreateItemsSourceService.GetAllCurrentWorklistItems();
        }

        public static void Stop()
      {
         _itemsLoaderTimer?.Dispose();
         _server?.Dispose();
      }


   }
}
