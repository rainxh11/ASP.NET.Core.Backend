using DicomServer.Helper;
using DicomServer.Models;
using DicomServerWorkList.Model;
using MongoDB.Bson;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DicomServer.Helpers
{
    public class StudyToWorklist
    {
        public static async Task<List<WorklistItem>> GetWorklist()
        {
			var config = ConfigHelper.GetConfig();

			var pipeline = new Template<RisStudy, BsonDocument>(@"[
				{
					$match: {
						<statusStudy> : { $ne : 'Canceled' },
						<statusWorklist> : { $nin: [ null, 'complete'] },
						<createdAt> : { $gte : new Date('<date_condition>') }
					}
				},

				{
					$lookup: {
						from: 'clients',
						localField: 'client',
						foreignField: '_id',
						as: 'client'
					}
				},

				{
					$unwind: {
						path: '$client'
					}
				},

				{
					$project: {
						_id:1,
						clientId: '$client._id',
						accession: { $dateToString: { date: '$client.createdAt', format: '%Y%m%d-%H%M%S' } },
						name:{ $toUpper :  { $concat : [ '$client.familyName', ' ', '$client.firstName'] } },
						gender: { $toUpper: '$client.gender' },
						birthdate: '$client.birthdate' ,
						studystatus: '$statusStudy',
						exam: { $ifNull: [ { $concat: ['$modality',' ','$examType'] }, ''] },
						modality: '$modality',
						doctor: { $toUpper: '$doctor' },
						date: '$createdAt',
						wlstatus: { $ifNull: [ '$statusWorklist', '' ] },
						wlmodality: { $ifNull: [ '$worklistModality', '' ] },
					}
				}, { $sort: { _id: -1 } }

			]")
			.Path(b => b.statusStudy)
			.Path(b => b.statusWorklist)
			.Path(b => b.createdAt)
			.Tag("date_condition", DateTime.Now.AddDays(config.WorklistConfig.ItemLifetime * -1).ToString("yyyy-MM-dd"));

			var studies = await DB.PipelineAsync<RisStudy, BsonDocument>(pipeline);

			var workList = studies.Select(x =>
			{

				var date = new Func<DateTime>(() => {
                    try
                    {
						return DateTime.Parse(x["birthdate"].AsBsonDateTime.ToString().Substring(0, 10)); 
					}
                    catch
                    {
						return new DateTime();
                    }
				})(); 

				var wlItem = new WorklistItem()
				{
					AccessionNumber = x["accession"].AsString,
					DateOfBirth = date,
					ExamDateAndTime = x["date"].ToLocalTime(),
					ExamDescription = x["exam"].AsString.ToLower().ToUpper(),
					PatientID = x["clientId"].AsObjectId.ToString(),
					StudyID = x["_id"].AsInt32.ToString(),
					StudyUID = x["_id"].AsInt32.ToString(),
					PerformingPhysician = "DR LAGHOUATI",
					PatientName = x["name"].AsString.ToLower().ToUpper(),
					Sex = x["gender"].AsString.Substring(0, 1).ToUpper(),
					ReferringPhysician = x["doctor"].AsString.ToLower().ToUpper(),
					ProcedureStepID = x["_id"].AsInt32.ToString(),
				};
                try
                {
					wlItem.Modality = x["modality"].AsString;

                }
				catch
                {

                }
				return wlItem;

			}).Where(x => ModalityHelper.AllowModality(x.Modality)).ToList();

			/*var workList = studies.Select(x =>
			{
				return new WorklistItem()
				{
					AccessionNumber = x["clientId"].AsObjectId.ToString(),
					DateOfBirth = x["birthdate"].ToLocalTime(),
					ExamDateAndTime = x["date"].ToLocalTime(),
					ExamDescription = x["exam"].AsString,
					PatientID = x["clientId"].AsObjectId.ToString(),
					StudyID = x["_id"].AsObjectId.ToString(),
					StudyUID = x["_id"].AsObjectId.ToString(),
					PerformingPhysician = "DR LAGHOUATI",
					PatientName = x["name"].AsString,
					Sex = x["gender"].AsString.Substring(0, 1).ToUpper(),
					ReferringPhysician = x["doctor"].AsString,
					ProcedureStepID = x["_id"].AsObjectId.ToString(),
					Modality = x["wlmodality"].AsString,
				};

			}).ToList();*/
			return workList;

		}
    }
}
