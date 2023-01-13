using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Entities;
using RisDocumentServer.Helpers.Models;
using RisReport.Library;
using RisReport.Library.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MoreLinq.Extensions;

namespace RisDocumentServer.Controllers
{
    public class RisReportController : Controller
    {
        public class ReportBody
        {
            public bool AllTime
            {
                get => DateTime.MinValue.ToString("yyyy-MM-dd") == StartDate &&
                       DateTime.MaxValue.ToString("yyyy-MM-dd") == EndDate ||
                       string.IsNullOrEmpty(StartDate) ||
                       string.IsNullOrEmpty(EndDate);
            }

            public string StartDate { get; set; } = DateTime.MinValue.ToString("yyyy-MM-dd");
            public string EndDate { get; set; } = DateTime.MaxValue.ToString("yyyy-MM-dd");
            public List<string> Users { get; set; } = new List<string>();

            public bool AllUsers
            {
                get => Users.Count == 0;
            }

            public void CheckDates()
            {
                try
                {
                    if (string.IsNullOrEmpty(this.StartDate) || string.IsNullOrEmpty(this.EndDate))
                    {
                        this.StartDate = DateTime.MinValue.ToString("yyyy-MM-dd");
                        this.EndDate = DateTime.MaxValue.ToString("yyyy-MM-dd");
                    }

                    var start = DateTime.Parse(this.StartDate);
                    var end = DateTime.Parse(this.EndDate);

                    if (start > end)
                    {
                        var temp = this.StartDate;
                        this.StartDate = this.EndDate;
                        this.EndDate = temp;
                    }

                    this.EndDate = DateTime.Parse(this.EndDate).AddHours(23).AddMinutes(59).AddSeconds(59)
                        .AddMilliseconds(999).ToString("yyyy-MM-ddTHH:mm:ss");
                }
                catch
                {
                }
            }
        }

        public class PrintDetbBody
        {
            public string ClientId { get; set; }
            public double PaidAmount { get; set; }
            public string PrinterIp { get; set; }
            public int PrinterPort { get; set; } = 9100;
            public string PaidBy { get; set; }
        }

        public class PrintBody
        {
            public string StudyId { get; set; }
            public double PaidAmount { get; set; }
            public string PrinterIp { get; set; }
            public int PrinterPort { get; set; } = 9100;
        }

        [HttpGet]
        [Route("documentserver/[controller]/debtreport")]
        public async Task<IActionResult> GetDebtReport()
        {
            byte[] report = new byte[] { };
            try
            {
                var stages = @"[
                    {
                      $addFields: {
                        debtAmount: {
                          $reduce: {
                            input: '$<debtPaymentInfo>',
                            initialValue: 0,
                            in: {
                              $cond: [
                                { $eq: ['$$this.status', 'out'] },
                                { $add: ['$$value', '$$this.amount'] },
                                { $subtract: ['$$value', '$$this.amount'] }
                              ]
                            }
                          }
                        }
                      }
                    },
                    {
                      $unwind: {
                        path: '$debtPaymentInfo',
                        preserveNullAndEmptyArrays: true
                      }
                    },

                    {
                      $lookup: {
                        from: 'users',
                        let: {
                          id: '$debtPaymentInfo.createdBy'
                        },
                        pipeline: [
                          {
                            $match: {
                              $expr: {
                                $eq: ['$_id', '$$id']
                              }
                            }
                          },
                          {
                            $project: {
                              _id: 1,
                              email: 1,
                              name: 1
                            }
                          }
                        ],
                        as: 'debtPaymentInfo.createdBy'
                      }
                    },
                    {
                      $unwind: {
                        path: '$debtPaymentInfo.createdBy',
                        preserveNullAndEmptyArrays: true
                      }
                    },
                    {
                      $group: {
                        _id: '$_id',
                        debtPaymentInfo: { $push: '$debtPaymentInfo' },
                        client: { $first: '$$ROOT' }
                      }
                    },
                    {
                      $replaceRoot: {
                        newRoot: {
                          $mergeObjects: ['$client', { debtPaymentInfo: '$debtPaymentInfo' }]
                        }
                      }
                    },
                    {
                      $addFields: {
                        lastDebtAt: {
                          $last: {
                            $filter: {
                              input: '$debtPaymentInfo',
                              as: 'debt',
                              cond: { $eq: ['$$debt.status', 'out'] }
                            }
                          }
                        }
                      }
                    },
                    {
                      $set: {
                        lastDebtAt: '$lastDebtAt.date'
                      }
                    },
                    {
                      $match: {
                        debtAmount: { $gt: 0 }
                      }
                    },
                    {
                      $sort: {
                        lastDebtAt: -1
                      }
                    }
                  ]";

                var pipeline = new Template<RisClient, RisDebtClient>(stages)
                    .Path(b => b.debtPaymentInfo);

                var clients = await DB.PipelineAsync(pipeline);

                report = await ReportHelper.CreateDetbReport(new RisDebtReport()
                {
                    Clients = clients
                });

                var response = new FileContentResult(report, "application/pdf")
                {
                    FileDownloadName = $"RIS_DebtReport_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm")}.pdf",
                    LastModified = DateTime.Now,
                };
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost]
        [Route("documentserver/[controller]/printreceipt")]
        public async Task<IActionResult> PrintReceipt([FromBody] PrintBody body)
        {
            try
            {
                var config = ConfigModel.GetConfig();

                var stages = @"[
					{
						$match: {
                            $expr: {
                                $or: [
                                { $eq: [ '$<group>', '<studyId>'] },
                                { $eq: [ { $toString : '$<_id>' }, '<studyId>' ] }
                                ]
                            }
                        }
					},
                    {
			            $set: {
			                paidBy: { $ifNull: ['$paidBy', '$createdBy' ]  }
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
							path: '$client',
							preserveNullAndEmptyArrays: false
						}
					},
{
						$lookup: {
							from: 'users',
							localField: 'paidBy',
							foreignField: '_id',
							as: 'paidBy'
						}
					},

					{
						$unwind: {
							path: '$paidBy',
							preserveNullAndEmptyArrays: false
						}
					},

					{
						$project: {
							_id: { $ifNull: [ { $toLong: '$group'},'$_id' ] },
							client:'$client',
							exam: { $concat: ['$modality', ' ', '$examType'] },
							paid: { $subtract: [ '$price', { $ifNull: ['$discount', 0] } ] },
							price: '$price',
							discount: { $ifNull: ['$discount', 0] },
							convention: '$conv',
							conventionPrice: { $ifNull: ['$convPrice', 0] },
							status: '$statusPayment',
							createdAt: '$createdAt',
							date: '$paidAt',
							paidBy: '$paidBy.name',
							products: '$product'
						}
					},

				]";

                var pipeline = new Template<RisStudy, RisReceipt>(stages)
                    .Path(b => b.group)
                    .Path(b => b._id)
                    .Tag("studyId", body.StudyId);

                var result = await DB.PipelineAsync(pipeline);

                var paid = result.First().status == "paid"
                    ? result.Sum(x => x.price) - result.Sum(x => Math.Abs(x.discount))
                    : body.PaidAmount;

                var invoiceModel = new InvoiceReportModel(config.QrCodeHost)
                {
                    Exams = result,
                    PaidAmount = paid,
                    PaidBy = result.Select(x => x.paidBy).First()
                };

                var image = await ReportHelper.CreateDebtReceipt(invoiceModel, savePath: config.SaveReceiptPath,
                    saveReceipt: config.SaveReceipt);


                var printer = new NetworkPrinter(new NetworkPrinterSettings()
                {
                    ConnectionString =
                        $"{body.PrinterIp}:{body.PrinterPort}"
                });


                var epson = new EPSON();
                epson.Initialize();

                printer.Write(
                    epson.LeftAlign(),
                    epson.BufferImage(image, isLegacy: true, maxWidth: 580, color: 0),
                    epson.SetImageDensity(true),
                    epson.WriteImageFromBuffer(),
                    epson.FeedLines(6),
                    epson.FullCut()
                );
                printer.Dispose();
                return Ok("Success");
            }
            catch (Exception ex)
            {
                Program.Logger.Error(ex.Message);
                Console.WriteLine(ex.Message);

                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("documentserver/[controller]/printdebtreceipt")]
        public async Task<IActionResult> PrintDebtReceipt([FromBody] PrintDetbBody body)
        {
            try
            {
                var config = ConfigModel.GetConfig();

                var client = await DB.Find<RisClient>().MatchID(body.ClientId).ExecuteFirstAsync();

                var invoiceModel = new InvoiceReportModel(config.QrCodeHost)
                {
                    ClientModel = client,
                    PaidBy = body.PaidBy,
                    PaidAmount = body.PaidAmount
                };
                var image = await ReportHelper.CreateDebtReceipt(invoiceModel, true, savePath: config.SaveReceiptPath,
                    saveReceipt: config.SaveReceipt);

                var printer = new NetworkPrinter(new NetworkPrinterSettings()
                {
                    ConnectionString =
                        $"{body.PrinterIp}:{body.PrinterPort}",
                });

                var epson = new EPSON();
                epson.Initialize();

                printer.Write(
                    epson.LeftAlign(),
                    epson.BufferImage(image, isLegacy: true, maxWidth: 580, color: 0),
                    epson.SetImageDensity(true),
                    epson.WriteImageFromBuffer(),
                    epson.FeedLines(6),
                    epson.FullCut()
                );
                printer.Dispose();
                return Ok("Success");
            }
            catch (Exception ex)
            {
                Program.Logger.Error(ex.Message);
                Console.WriteLine(ex.Message);

                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("documentserver/[controller]/printincomereport")]
        public async Task<IActionResult> PrintIncomeReport([FromBody] ReportBody body)
        {
            try
            {
                body.CheckDates();

                if (body.Users.Count == 1)
                {
                    body.Users = body.Users.Repeat(2).ToList();
                }

                var users = body.AllUsers
                    ? $"ObjectId('{ObjectId.GenerateNewId()}')"
                    : body.Users.Aggregate("", (a, c) => a + $", ObjectId('{c}')").Trim(',').Trim();

                var stages = @"[
					{
						$match: {
                            <statusPayment>: { $in: ['paid', 'debt'] },                           
                        }
					},
                    {
			            $set: {
                            paidBy: { $ifNull: ['$paidBy', '$createdBy' ] },
                            paidAt: { $ifNull: ['$paidAt', '$createdAt' ] },
                            allUsers: <allUsers>,
                            allTime: <allTime>
                        }
		            },
                    {
                        $match: {
                            $and: [
                             { $or: [ { paidAt : { $gte: ISODate('<startDate>'), $lte: ISODate('<endDate>') } }, { allTime : true } ] },
                             { $or: [ { paidBy : { $in: [ <users> ] } }, { allUsers : true } ] }
                             ]
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
							path: '$client',
							preserveNullAndEmptyArrays: false
						}
					},
{
						$lookup: {
							from: 'users',
							localField: 'paidBy',
							foreignField: '_id',
							as: 'paidBy'
						}
					},

					{
						$unwind: {
							path: '$paidBy',
							preserveNullAndEmptyArrays: false
						}
					},
                    {
                        $unset: [ 'client.debtPaymentInfo' ]
                    },
					{
						$project: {
							_id: '$_id',
							client:'$client',
							exam: { $concat: ['$modality', ' ', '$examType'] },
							paid: { $subtract: [ '$price', { $ifNull: ['$discount', 0] } ] },
							price: { $ifNull: ['$price', 0 ] },
							discount: { $ifNull: ['$discount', 0] },
							convention: { $toUpper: '$conv' },
							conventionPrice: { $ifNull: ['$convPrice', 0] },
							status: '$statusPayment',
							createdAt: '$createdAt',
							date: '$paidAt',
							paidBy: '$paidBy.name',
							products: '$product'
						}
					},

				]";


                var pipeline = new Template<RisStudy, RisReceipt>(stages)
                    .Path(b => b.statusPayment)
                    .Tag("startDate", body.StartDate)
                    .Tag("endDate", body.EndDate)
                    .Tag("allUsers", body.AllUsers.ToString().ToLower())
                    .Tag("allTime", body.AllTime.ToString().ToLower())
                    .Tag("users", users);

                var result = await DB.PipelineAsync(pipeline);


                var incomeModel = new RisIncomeReport()
                {
                    Studies = result,
                    StartDate = DateTime.Parse(body.StartDate),
                    EndDate = DateTime.Parse(body.EndDate),
                    AllTime = body.AllTime
                };

                var report = await ReportHelper.CreateIncomeReport(incomeModel);

                var response = new FileContentResult(report, "application/pdf")
                {
                    FileDownloadName = $"RIS_IncomeReport_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.pdf",
                    LastModified = DateTime.Now
                };
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost]
        [Route("documentserver/[controller]/printexamreport")]
        public async Task<IActionResult> PrintExamReport([FromBody] ReportBody body)
        {
            try
            {
                body.CheckDates();


                if (body.Users.Count == 1)
                {
                    body.Users = body.Users.Repeat(2).ToList();
                }

                var users = body.AllUsers
                    ? $"ObjectId('{ObjectId.GenerateNewId()}')"
                    : body.Users.Aggregate("", (a, c) => a + $", ObjectId('{c}')").Trim(',').Trim();

                var stages = @"[
					{
						$match: {
                            <statusPayment>: { $in: ['paid', 'debt'] },                           
                        }
					},
                    {
			            $set: {
                            paidBy: { $ifNull: ['$paidBy', '$createdBy' ] },
                            paidAt: { $ifNull: ['$paidAt', '$createdAt' ] },
                            allUsers: <allUsers>,
                            allTime: <allTime>
                        }
		            },
                    {
                        $match: {
                            $and: [
                             { $or: [ { paidAt : { $gte: ISODate('<startDate>'), $lte: ISODate('<endDate>') } }, { allTime : true } ] },
                             { $or: [ { paidBy : { $in: [ <users> ] } }, { allUsers : true } ] }
                             ]
                        }
                    },
                    {
			            $unwind: {
			                path: '$product',
			                includeArrayIndex: 'index',
			                preserveNullAndEmptyArrays: true
			            }
		            },
                    {
						$lookup: {
							from: 'users',
							localField: 'paidBy',
							foreignField: '_id',
							as: 'paidBy'
						}
					},

					{
						$unwind: {
							path: '$paidBy',
							preserveNullAndEmptyArrays: false
						}
					},
                    {
			            $group: {
			                _id: { createdBy: '$createdBy.name', exam: { $concat: [ '$modality', ' ', '$examType' ] }, date: { $dateToString: { date: '$paidAt', format: '%Y-%m-%d'}}, status: '$statusPayment' },
			                examCount: { $sum: { $cond: [ { $eq: ['$index', 0]}, 0, 1 ] } },
			                paid: { $sum: { $cond: [ { $eq: ['$index', 0]}, 0,'$price'] } },
			                discount: { $sum: { $cond: [ { $eq: ['$index', 0]}, 0,'$discount'] } },
			                convPrice: { $sum: { $cond: [ { $eq: ['$index', 0]}, 0,'$convPrice'] } },
			                productSum: { $sum: { $multiply: ['$product.price', '$product.quantityP']} }
			            }
		            },
		            {
			            $project: {
			                _id: '$_id.date',
			                exam: '$_id.exam',
			                createdBy: '$_id.createdBy',
			                examCount: '$examCount',
			                price: '$paid',
			                discount: '$discount',
			                convPrice: '$convPrice',
			                status: '$_id.status',
			                productSum: '$productSum'
			            }
		            }

				]";


                var pipeline = new Template<RisStudy, ExamReport>(stages)
                    .Path(b => b.statusPayment)
                    .Tag("startDate", body.StartDate)
                    .Tag("endDate", body.EndDate)
                    .Tag("allUsers", body.AllUsers.ToString().ToLower())
                    .Tag("allTime", body.AllTime.ToString().ToLower())
                    .Tag("users", users);

                var result = await DB.PipelineAsync(pipeline);


                var incomeModel = new RisExamsReport()
                {
                    Exams = result,
                    StartDate = DateTime.Parse(body.StartDate),
                    EndDate = DateTime.Parse(body.EndDate),
                    AllTime = body.AllTime
                };

                /*var report = await ReportHelper.CreateIncomeReport(incomeModel);

                var response = new FileContentResult(report, "application/pdf")
                {
                    FileDownloadName = $"RIS_IncomeReport_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.pdf",
                    LastModified = DateTime.Now,
                };
                return response;*/
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}