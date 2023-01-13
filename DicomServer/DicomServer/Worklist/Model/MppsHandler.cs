// Copyright (c) 2012-2020 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using System;
using System.Collections.Generic;
using System.Linq;
using FellowOakDicom.Log;
using DicomServer;
using DicomServer.Models;
using MongoDB.Entities;

namespace DicomServerWorkList.Model
{

    /// <summary>
    /// An implementation of IMppsSource, that does only logging but does not store the MPPS messages
    /// </summary>
    class MppsHandler : IMppsSource
    {

        public static Dictionary<string, WorklistItem> PendingProcedures { get; } = new Dictionary<string, WorklistItem>();

        private readonly ILogger _logger;


        public MppsHandler(ILogger logger)
        {
            _logger = logger;
        }


        public bool SetInProgress(string sopInstanceUID, string procedureStepId)
        {
            try
            {
                var workItem = WorklistServer.CurrentWorklistItems
                .FirstOrDefault(w => w.ProcedureStepID == procedureStepId);
                if (workItem == null)
                {
                    // the procedureStepId provided cannot be found any more, so the data is invalid or the 
                    // modality tries to start a procedure that has been deleted/changed on the ris side...
                    return false;
                }

                // now here change the sate of the procedure in the database or do similar stuff...
                _logger.Info($"Procedure with id {workItem.ProcedureStepID} of Patient {workItem.PatientName} is started");

                // remember the sopInstanceUID and store the worklistitem to which the sopInstanceUID belongs. 
                // You should do this more permanent like in database or in file
                PendingProcedures.Add(sopInstanceUID, workItem);
                Run.Sync(() => DB.Update<RisStudy>().MatchID(sopInstanceUID).Modify(x => x.statusWorklist, "inProgress").ExecuteAsync());


                return true;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
            
        }


        public bool SetDiscontinued(string sopInstanceUID, string reason)
        {
            try
            {
                if (!PendingProcedures.ContainsKey(sopInstanceUID))
                {
                    // there is no pending procedure with this sopInstanceUID!
                    return false;
                }
                var workItem = PendingProcedures[sopInstanceUID];

                // now here change the sate of the procedure in the database or do similar stuff...
                _logger.Info($"Procedure with id {workItem.ProcedureStepID} of Patient {workItem.PatientName} is discontinued for reason {reason}");

                // since the procedure was stopped, we remove it from the list of pending procedures
                PendingProcedures.Remove(sopInstanceUID);
                Run.Sync(() => DB.Update<RisStudy>().MatchID(sopInstanceUID).Modify(x => x.statusWorklist, "cancelled").ExecuteAsync());

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }


        public bool SetCompleted(string sopInstanceUID, string doseDescription, List<string> affectedInstanceUIDs)
        {
            try
            {
                if (!PendingProcedures.ContainsKey(sopInstanceUID))
                {
                    // there is no pending procedure with this sopInstanceUID!
                    return false;
                }
                var workItem = PendingProcedures[sopInstanceUID];

                // now here change the sate of the procedure in the database or do similar stuff...
                _logger.Info($"Procedure with id {workItem.ProcedureStepID} of Patient {workItem.PatientName} is completed");

                // the MPPS completed message contains some additional informations about the performed procedure.
                // this informations are very vendor depending, so read the DICOM Conformance Statement or read
                // the DICOM logfiles to see which informations the vendor sends

                // since the procedure was completed, we remove it from the list of pending procedures
                PendingProcedures.Remove(sopInstanceUID);
                Run.Sync(() => DB.Update<RisStudy>().MatchID(sopInstanceUID).Modify(x => x.statusWorklist, "complete").ExecuteAsync());

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }


    }
}
