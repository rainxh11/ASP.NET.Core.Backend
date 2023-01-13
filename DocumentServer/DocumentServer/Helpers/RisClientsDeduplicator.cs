using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RisReport.Library.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System.Text.RegularExpressions;
using Serilog;
using RisDocumentServer.Helpers.Models;

namespace RisDocumentServer.Helpers
{
    public class DuplicationResult
    {
        public string ClientName { get; set; }
        public List<ObjectId> Ids { get; set; }
        public List<ObjectId> ToRemove { get => Ids.Where(x => x != First).ToList(); }
        public List<int> Studies { get; set; }

        public List<ClientDebt> Debt { get; set; }
        public ObjectId First { get; set; }
    }

    public class RisClientsDeduplicator
    {
        public static async Task CleanClientNames()
        {
            try
            {
                /*var pipeline = new Template<RisClient>(@"[
				    {
					    $match: {
                            $expr: {
                                $regexMatch: {
                                    input: { $concat: ['$<familyName>', ' ', '$<firstName>'] },
                                    regex: /\s{2,}/i
                                }
                        }}
			        }
			    ]")
                .Path(b => b.familyName)
                .Path(b => b.firstName);
                */
                var pipeline = new Template<RisClient>(@"[
				    {
					    $project:
                        {
                            <familyName>:1,
                            <firstName>:1
                        }
			        }
			    ]")
                .Path(b => b.familyName)
                .Path(b => b.firstName);

                var clients = await DB.PipelineAsync(pipeline);
                var config = ConfigModel.GetConfig();

                clients = clients.Select(x =>
                {
                    x.familyName = x.familyName.ToUpper();

                    config.ClientNameReplacements.ForEach(replacement =>
                    {
                        try
                        {
                            x.familyName = replacement.ReplaceString(x.familyName);
                        }
                        catch
                        {
                        }
                    });

                    x.familyName = new Regex("[ ]{2,}", RegexOptions.None).Replace(x.familyName, " ").Trim();

                    return x;
                }).ToList();

                using (var transaction = DB.Transaction())
                {
                    foreach (var item in clients)
                    {
                        await DB.Update<RisClient>(transaction.Session)
                           .MatchID(item.ID)
                           .Modify(x => x.familyName, item.familyName)
                           .ExecuteAsync();
                    }
                    await transaction.CommitAsync();
                }
            }
            catch (Exception ex)
            {
                Program.Logger.Error(ex.Message);
                Console.WriteLine(ex.Message);
            }
        }

        public static async Task Deduplicate()
        {
            try
            {
                var pipeline = new Template<RisClient, BsonDocument>(@"[
				{
					$group: {
						_id:  { $trim: { input: { $concat: ['$<familyName>', ' ', '$<firstName>'] } } }     ,
						ids: { $push: '$_id' }
					}
				},

				{
					$match: {
						ids: { $not : { $size: 1 } },
					}
				},
						{
					$lookup: {
					   from: 'studies',
					   let: {
						   ids: '$ids'
					   },
					   pipeline: [
					   {
						   $match: {
							   $expr: {
								   $in: [ '$client','$$ids']
							   }
						   }
					   },
					   {
						   $project: { _id: '$_id' }
					   }
					   ],
					   as: 'studies'
					}
				},

				{
					$lookup: {
					   from: 'clients',
					   let: {
						   ids: '$ids'
					   },
					   pipeline: [
					   {
						   $match: {
							   $expr: {
								   $in: [ '$_id','$$ids']
							   }
						   }
					   },
					   {
						   $unwind: { path: '$debtPaymentInfo' }
					   },
					   {
						   $project: { _id: '$debtPaymentInfo' }
					   }
					   ],
					   as: 'debt'
					}
				},
				{
					$set: {
						first: { $first: '$ids' } }
				}

			]")
                    .Path(b => b.familyName)
                    .Path(b => b.firstName);

                var result = await DB.PipelineAsync(pipeline);

                var results = result.Select(doc =>
                 {
                     var studies = new List<int>();
                     var debt = new List<ClientDebt>();
                     try
                     {
                         studies = doc["studies"].AsBsonArray.Select(x => x.AsBsonDocument["_id"].AsInt32).ToList();
                     }
                     catch
                     {
                     }
                     try
                     {
                         debt = doc["debt"].AsBsonArray.Select(x => BsonSerializer.Deserialize<ClientDebt>(x.AsBsonDocument["_id"].AsBsonDocument)).ToList();
                     }
                     catch
                     {
                     }
                     return new DuplicationResult()
                     {
                         ClientName = doc["_id"].AsString,
                         Ids = doc["ids"].AsBsonArray.Select(x => x.AsObjectId).ToList(),
                         Studies = studies,
                         First = doc["first"].AsObjectId,
                         Debt = debt
                     };
                 }).ToList();

                using (var transaction = DB.Transaction())
                {
                    foreach (var item in results)
                    {
                        await DB.Update<RisClient>(transaction.Session)
                        .MatchID(item.First.ToString())
                        .Modify(x => x.debtPaymentInfo, item.Debt)
                        .ExecuteAsync();
                        await DB.DeleteAsync<RisClient>(item.ToRemove.Select(x => x.ToString()), transaction.Session);
                        await DB.Update<RisStudy>(transaction.Session)
                            .Match(x => item.Studies.Contains(x._id))
                            .Modify(x => x.client, item.First)
                            .ExecuteAsync();
                    }
                    await transaction.CommitAsync();
                }
            }
            catch (Exception ex)
            {
                Program.Logger.Error(ex.Message);
                Console.WriteLine(ex.Message);
            }
        }
    }
}