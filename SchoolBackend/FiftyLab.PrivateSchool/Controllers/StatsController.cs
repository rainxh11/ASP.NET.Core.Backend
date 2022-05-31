using FiftyLab.PrivateSchool.Helpers;
using FiftyLab.PrivateSchool.Hubs;
using FiftyLab.PrivateSchool.Response;
using FiftyLab.PrivateSchool.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Entities;

namespace FiftyLab.PrivateSchool.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class StatsController : ControllerBase
    {
        private readonly ILogger<StatsController> _logger;
        private readonly IHubContext<PrivateSchoolHub> _mailHub;
        private IBackgroundJobClient _backgroundJobs;
        private readonly IConfiguration _config;
        private readonly IIdentityService _identityService;
        private readonly LoginInfoSaver _loginSaver;
        private NotificationService _notificationService;
        private readonly ITokenService _tokenService;

        public StatsController(
            ILogger<StatsController> logger,
            IIdentityService identityService,
            ITokenService tokenService,
            IHubContext<PrivateSchoolHub> mailHub,
            IBackgroundJobClient bgJobs,
            NotificationService nservice,
            LoginInfoSaver loginSaver,
            IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _tokenService = tokenService;
            _identityService = identityService;
            _mailHub = mailHub;
            _backgroundJobs = bgJobs;
            _notificationService = nservice;
            _loginSaver = loginSaver;
        }

        [Authorize(Roles = $"{AccountRole.Admin}")]
        [HttpGet]
        [Route("payment/interval")]
        public async Task<IActionResult> GetPaymentStatsInterval(string start, string end)
        {
            try
            {
                if (DateTime.Parse(start) > DateTime.Parse(end))
                {
                    var temp = start;
                    start = end;
                    end = temp;
                }

                var startDate = DateTime.Parse(start);
                var endDate = DateTime.Parse(end);

                start = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc)
                    .ToString("O");
                end = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc)
                    .ToString("O");


                var template = new Template<Transaction, BsonDocument>(@"[
                            {
                                $match: {
                                    $expr: {
                                        $and: [{
                                                $eq: ['$<Enabled>', true]
                                            },
                                            {
                                                    $gte: ['$<CreatedOn>', new Date('<start>')]
                                            },
                                            {
                                                    $lte: ['$<CreatedOn>', new Date('<end>')]
                                            },
                                        ]
                                    }
                                }
                            },
                            {
                                $group: {
                                    _id: { Type: '$Type', Date: { $dateToString: { date: '$CreatedOn', format: '%Y-%m-%d'} } },
                                    Amount: {
                                        $sum: '$Amount'
                                    }
                                }
                            },
                            {
                                $group: {
                                    _id: '$_id.Date',
                                    Discount: {
                                        $sum: {
                                            $cond: [{
                                                $eq: ['$_id.Type', 'Discount']
                                            }, { $toDouble: '$Amount' }, { $toDouble: 0 }]
                                        }
                                    },
                                    Debt: {
                                        $sum: {
                                            $cond: [{
                                                $eq: ['$_id.Type', 'Debt']
                                            }, { $toDouble: '$Amount' }, { $toDouble: 0 }]
                                        }
                                    },
                                    Paid: {
                                        $sum: {
                                            $cond: [{
                                                $eq: ['$_id.Type', 'Payment']
                                            }, { $toDouble: '$Amount' }, { $toDouble: 0 }]
                                        }
                                    }
                                }
                            },
                        ]")
                    .Path(x => x.CreatedOn)
                    .Path(x => x.Enabled)
                    .Tag("start", start)
                    .Tag("end", end);

                var pipeline = await DB.PipelineAsync(template);

                return Ok(new StatsResultResponse<IEnumerable<object>, object>(
                    pipeline.Select(x => BsonSerializer.Deserialize<object>(x)),
                    new
                    {
                        Discount = pipeline.Sum(x => x["Discount"].AsDouble),
                        Debt = pipeline.Sum(x => x["Debt"].AsDouble),
                        Paid = pipeline.Sum(x => x["Paid"].AsDouble)
                    },
                    $"{start} -> {end}"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        ////----------------------------------------------------------------------------------------///
        [Authorize(Roles = $"{AccountRole.Admin}")]
        [HttpGet]
        [Route("payment")]
        public async Task<IActionResult> GetPaymentStats()
        {
            try
            {
                var template = new Template<Transaction, BsonDocument>(@"[
                            {
                                $match: { <Enabled>: true }
                            },
                            {
                                $group: {
                                    _id: { Type: '$Type', Date: { $dateToString: { date: '$<CreatedOn>', format: '%Y-%m-%d'} } },
                                    Amount: {
                                        $sum: '$Amount'
                                    }
                                }
                            },
                            {
                                $group: {
                                    _id: '$_id.Date',
                                    Discount: {
                                        $sum: {
                                            $cond: [{
                                                $eq: ['$_id.Type', 'Discount']
                                            }, { $toDouble: '$Amount' }, { $toDouble: 0 }]
                                        }
                                    },
                                    Debt: {
                                        $sum: {
                                            $cond: [{
                                                $eq: ['$_id.Type', 'Debt']
                                            }, { $toDouble: '$Amount' }, { $toDouble: 0 }]
                                        }
                                    },
                                    Paid: {
                                        $sum: {
                                            $cond: [{
                                                $eq: ['$_id.Type', 'Payment']
                                            }, { $toDouble: '$Amount' }, { $toDouble: 0 }]
                                        }
                                    }
                                }
                            },
                        ]")
                    .Path(x => x.CreatedOn)
                    .Path(x => x.Enabled);

                var pipeline = await DB.PipelineAsync(template);

                return Ok(new StatsResultResponse<IEnumerable<object>, object>(
                    pipeline.Select(x => BsonSerializer.Deserialize<object>(x)),
                    new
                    {
                        Discount = pipeline.Sum(x => x["Discount"].AsDouble),
                        Debt = pipeline.Sum(x => x["Debt"].AsDouble),
                        Paid = pipeline.Sum(x => x["Paid"].AsDouble)
                    },
                    $"All Time"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        ////----------------------------------------------------------------------------------------///
        [Authorize(Roles = $"{AccountRole.Admin}")]
        [HttpGet]
        [Route("formation/interval")]
        public async Task<IActionResult> GetFormationStatsInterval(string start, string end)
        {
            try
            {
                if (DateTime.Parse(start) > DateTime.Parse(end))
                {
                    var temp = start;
                    start = end;
                    end = temp;
                }

                var startDate = DateTime.Parse(start);
                var endDate = DateTime.Parse(end);

                start = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc)
                    .ToString("O");
                end = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc)
                    .ToString("O");


                var formationCount = await DB.Fluent<Invoice>()
                    .Match(new BsonDocument("$expr", new BsonDocument("$and", new BsonArray()
                    {
                        new BsonDocument("$eq", new BsonArray { "$Enabled", true }),
                        new BsonDocument("$gte",
                            new BsonArray { "$CreatedOn", new BsonDateTime(DateTime.Parse(start)) }),
                        new BsonDocument("$lte", new BsonArray { "$CreatedOn", new BsonDateTime(DateTime.Parse(end)) }),
                    })))
                    .Group(new BsonDocument
                    {
                        {
                            "_id", new BsonDocument
                            {
                                { "Formation", "$Formation.Name" },
                                {
                                    "Date", new BsonDocument("$dateToString", new BsonDocument
                                    {
                                        { "date", "$CreatedOn" },
                                        { "format", "%Y-%m-%d" }
                                    })
                                },
                            }
                        },
                        { "Count", new BsonDocument("$sum", 1) }
                    })
                    .ToListAsync();

                var invoiceTransaction = await DB.Database(_config["MongoDb:DatabaseName"])
                    .GetCollection<BsonDocument>("[(Invoices)Invoice~Transaction(Transactions)]")
                    .Aggregate()
                    .Lookup("Invoice", "ParentID", "_id", "ParentID")
                    .Lookup("Transaction", "ChildID", "_id", "ChildID")
                    .Unwind("ChildID")
                    .Unwind("ParentID")
                    .Match(new BsonDocument("$expr", new BsonDocument("$and", new BsonArray()
                    {
                        new BsonDocument("$eq", new BsonArray() { "$ParentID.Enabled", true }),
                        new BsonDocument("$eq", new BsonArray() { "$ChildID.Enabled", true }),
                        new BsonDocument("$gte",
                            new BsonArray { "$ChildID.CreatedOn", new BsonDateTime(DateTime.Parse(start)) }),
                        new BsonDocument("$lte",
                            new BsonArray { "$ChildID.CreatedOn", new BsonDateTime(DateTime.Parse(end)) }),
                    })))
                    .Group(new BsonDocument
                    {
                        {
                            "_id", new BsonDocument
                            {
                                { "Type", "$ChildID.Type" },
                                { "Formation", "$ParentID.Formation.Name" },
                                {
                                    "Date", new BsonDocument("$dateToString", new BsonDocument
                                    {
                                        { "date", "$ChildID.CreatedOn" },
                                        { "format", "%Y-%m-%d" }
                                    })
                                },
                            }
                        },
                        { "Amount", new BsonDocument("$sum", "$ChildID.Amount") }
                    })
                    .Group(new BsonDocument
                        {
                            {
                                "_id", new BsonDocument
                                {
                                    { "Formation", "$_id.Formation" },
                                    { "Date", "$_id.Date" }
                                }
                            },
                            {
                                "Amount", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray()
                                {
                                    new BsonDocument("$eq", new BsonArray() { "$_id.Type", "Payment" }),
                                    new BsonDocument("$toDouble", "$Amount"),
                                    new BsonDouble(0)
                                }))
                            }
                        }
                    ).ToListAsync();
                var formationList = formationCount
                    .Select(x => new
                    {
                        Count = x["Count"].AsInt32,
                        Date = x["_id"].AsBsonDocument["Date"].AsString,
                        Formation = x["_id"].AsBsonDocument["Formation"].AsString
                    });

                var invoiceList = invoiceTransaction
                    .Select(x => new
                    {
                        Amount = x["Amount"].AsDouble,
                        Date = x["_id"].AsBsonDocument["Date"].AsString,
                        Formation = x["_id"].AsBsonDocument["Formation"].AsString
                    });
                var result = invoiceList
                    .Select(x => new
                    {
                        Count = formationList
                            .Where(f => f.Formation == x.Formation && f.Date == x.Date)
                            .Sum(f => f.Count),
                        x.Formation,
                        x.Date,
                        x.Amount
                    })
                    .GroupBy(x => x.Formation)
                    .Select(x => new
                    {
                        Formation = x.Key,
                        Amount = x.Sum(f => f.Amount),
                        Count = x.Sum(f => f.Count)
                    });

                /*var result = invoiceTransaction
                    .Join(
                        formationCount,
                        transaction => transaction["_id"].AsBsonDocument["Date"],
                        invoice => invoice["_id"].AsBsonDocument["Date"],
                        (transaction, invoice) => new
                        {
                            Count = invoice["Count"].AsInt32,
                            Amount = transaction["Amount"].AsDouble,
                            Date = transaction["_id"].AsBsonDocument["Date"].AsString,
                            Formation = transaction["_id"].AsBsonDocument["Formation"].AsString
                        })
                    .DistinctBy(x => x.Date + x.Formation)
                    .GroupBy(x => x.Formation)
                    .Select(x => new
                    {
                        Count = x.Sum(z => z.Count),
                        Amount = x.Sum(z => z.Amount),
                        Formation = x.Key
                    });*/

                return Ok(new ResultResponse<IEnumerable<object>, string>(
                    result,
                    $"{start} -> {end}"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        ////----------------------------------------------------------------------------------------///
        [Authorize(Roles = $"{AccountRole.Admin}")]
        [HttpGet]
        [Route("formation")]
        public async Task<IActionResult> GetFormationStats()
        {
            try
            {
                var formationCount = await DB.Fluent<Invoice>()
                    .Match(new BsonDocument("$expr", new BsonDocument("$eq", new BsonArray { "$Enabled", true })))
                    .Group(new BsonDocument
                    {
                        {
                            "_id", new BsonDocument
                            {
                                { "Formation", "$Formation.Name" },
                                {
                                    "Date", new BsonDocument("$dateToString", new BsonDocument
                                    {
                                        { "date", "$CreatedOn" },
                                        { "format", "%Y-%m-%d" }
                                    })
                                },
                            }
                        },
                        { "Count", new BsonDocument("$sum", 1) }
                    })
                    .ToListAsync();

                var invoiceTransaction = await DB.Database(_config["MongoDb:DatabaseName"])
                    .GetCollection<BsonDocument>("[(Invoices)Invoice~Transaction(Transactions)]")
                    .Aggregate()
                    .Lookup("Invoice", "ParentID", "_id", "ParentID")
                    .Lookup("Transaction", "ChildID", "_id", "ChildID")
                    .Unwind("ChildID")
                    .Unwind("ParentID")
                    .Match(new BsonDocument("$expr", new BsonDocument("$and", new BsonArray()
                    {
                        new BsonDocument("$eq", new BsonArray() { "$ParentID.Enabled", true }),
                        new BsonDocument("$eq", new BsonArray() { "$ChildID.Enabled", true })
                    })))
                    .Group(new BsonDocument
                    {
                        {
                            "_id", new BsonDocument
                            {
                                { "Type", "$ChildID.Type" },
                                { "Formation", "$ParentID.Formation.Name" },
                                {
                                    "Date", new BsonDocument("$dateToString", new BsonDocument
                                    {
                                        { "date", "$ChildID.CreatedOn" },
                                        { "format", "%Y-%m-%d" }
                                    })
                                },
                            }
                        },
                        { "Amount", new BsonDocument("$sum", "$ChildID.Amount") }
                    })
                    .Group(new BsonDocument
                        {
                            {
                                "_id", new BsonDocument
                                {
                                    { "Formation", "$_id.Formation" },
                                    { "Date", "$_id.Date" }
                                }
                            },
                            {
                                "Amount", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray()
                                {
                                    new BsonDocument("$eq", new BsonArray() { "$_id.Type", "Payment" }),
                                    new BsonDocument("$toDouble", "$Amount"),
                                    new BsonDouble(0)
                                }))
                            }
                        }
                    ).ToListAsync();

                var formationList = formationCount
                    .Select(x => new
                    {
                        Count = x["Count"].AsInt32,
                        Date = x["_id"].AsBsonDocument["Date"].AsString,
                        Formation = x["_id"].AsBsonDocument["Formation"].AsString
                    });

                var invoiceList = invoiceTransaction
                    .Select(x => new
                    {
                        Amount = x["Amount"].AsDouble,
                        Date = x["_id"].AsBsonDocument["Date"].AsString,
                        Formation = x["_id"].AsBsonDocument["Formation"].AsString
                    });
                var result = invoiceList
                    .Select(x => new
                    {
                        Count = formationList
                            .Where(f => f.Formation == x.Formation && f.Date == x.Date)
                            .Sum(f => f.Count),
                        x.Formation,
                        x.Date,
                        x.Amount
                    })
                    .GroupBy(x => x.Formation)
                    .Select(x => new
                    {
                        Formation = x.Key,
                        Amount = x.Sum(f => f.Amount),
                        Count = x.Sum(f => f.Count)
                    });

                /*var result = invoiceTransaction
                    .Join(
                        formationCount,
                        transaction => transaction["_id"].AsBsonDocument["Date"],
                        invoice => invoice["_id"].AsBsonDocument["Date"],
                        (transaction, invoice) => new
                        {
                            Count = invoice["Count"].AsInt32,
                            Amount = transaction["Amount"].AsDouble,
                            Date = transaction["_id"].AsBsonDocument["Date"].AsString,
                            Formation = transaction["_id"].AsBsonDocument["Formation"].AsString
                        })
                    .DistinctBy(x => x.Date + x.Formation)
                    .GroupBy(x => x.Formation)
                    .Select(x => new
                    {
                        Count = x.Sum(z => z.Count),
                        Amount = x.Sum(z => z.Amount),
                        Formation = x.Key
                    });*/

                return Ok(new ResultResponse<IEnumerable<object>, string>(
                    result,
                    $"All Time"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        ////----------------------------------------------------------------------------------------///
        [Authorize(Roles = $"{AccountRole.Admin}")]
        [HttpGet]
        [Route("student/interval")]
        public async Task<IActionResult> GetStudentStatsInterval(string start, string end)
        {
            try
            {
                if (DateTime.Parse(start) > DateTime.Parse(end))
                {
                    var temp = start;
                    start = end;
                    end = temp;
                }

                var startDate = DateTime.Parse(start);
                var endDate = DateTime.Parse(end);

                start = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc)
                    .ToString("O");
                end = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc)
                    .ToString("O");


                var template = new Template<Student, BsonDocument>(@"[
		                {
			                $match: {
			                    $expr: {
			                        $and: [
			                            { $gte: ['$<CreatedOn>', new Date('<start>') ] },
			                            { $lte: ['$<CreatedOn>', new Date('<end>') ] }
			                        ]
			                    }
			                }
		                },
		                {
			                $group: {
			                    _id: { Gender: '$Gender', Date: { $dateToString: { date: '$CreatedOn', format: '%Y-%m-%d'} } },
			                    Count: { $sum: 1 }
			                }
		                },
		                {
			                $group: {
			                    _id: '$_id.Date',
			                    Male: { $sum: { $cond: [ {$eq:['$_id.Gender', 'Male'] }, '$Count', 0 ] } },
			                    Female: { $sum: { $cond: [ {$eq:['$_id.Gender', 'Female'] }, '$Count', 0] } }
			                }
		                },
		                {
			                $set: {
			                    Date: '$_id'
			                }
		                },
		                {
			                $unset: [ '_id' ]
		                },
	                ]")
                    .Path(x => x.CreatedOn)
                    .Tag("start", start)
                    .Tag("end", end);

                var pipeline = await DB.PipelineAsync(template);

                return Ok(new StatsResultResponse<IEnumerable<object>, object>(
                    pipeline.Select(x => BsonSerializer.Deserialize<object>(x)),
                    new
                    {
                        Male = pipeline.Sum(x => x["Male"].AsInt32),
                        Female = pipeline.Sum(x => x["Female"].AsInt32),
                        Total = pipeline.Sum(x => x["Male"].AsInt32) + pipeline.Sum(x => x["Female"].AsInt32)
                    },
                    $"{start} -> {end}"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        ////----------------------------------------------------------------------------------------///
        [Authorize(Roles = $"{AccountRole.Admin}")]
        [HttpGet]
        [Route("student")]
        public async Task<IActionResult> GetStudentStats()
        {
            try
            {
                var template = new Template<Student, BsonDocument>(@"[
		                {
			                $group: {
			                    _id: { <Gender>: '$<Gender>', Date: { $dateToString: { date: '$<CreatedOn>', format: '%Y-%m-%d'} } },
			                    Count: { $sum: 1 }
			                }
		                },
		                {
			                $group: {
			                    _id: '$_id.Date',
			                    Male: { $sum: { $cond: [ {$eq:['$_id.Gender', 'Male'] }, '$Count', 0 ] } },
			                    Female: { $sum: { $cond: [ {$eq:['$_id.Gender', 'Female'] }, '$Count', 0] } }
			                }
		                },
		                {
			                $set: {
			                    Date: '$_id'
			                }
		                },
		                {
			                $unset: [ '_id' ]
		                },
	                ]")
                    .Path(x => x.CreatedOn)
                    .Path(x => x.Gender);

                var pipeline = await DB.PipelineAsync(template);

                return Ok(new StatsResultResponse<IEnumerable<object>, object>(
                    pipeline.Select(x => BsonSerializer.Deserialize<object>(x)),
                    new
                    {
                        Male = pipeline.Sum(x => x["Male"].AsInt32),
                        Female = pipeline.Sum(x => x["Female"].AsInt32),
                        Total = pipeline.Sum(x => x["Male"].AsInt32) + pipeline.Sum(x => x["Female"].AsInt32)
                    },
                    $"All Time"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        ////----------------------------------------------------------------------------------------///
        [Authorize(Roles = $"{AccountRole.Admin}")]
        [HttpGet]
        [Route("payment/byperiod")]
        public async Task<IActionResult> GetPaymentStatsGroupedByPeriod(string period)
        {
            try
            {
                string periodFormat = "";
                switch (period)
                {
                    case "day":
                        periodFormat = "%Y-%m-%d";
                        break;
                    default:
                    case "month":
                        periodFormat = "%Y-%m";
                        break;
                    case "week":
                        periodFormat = "%Y-%U";
                        break;
                    case "year":
                        periodFormat = "%Y";
                        break;
                }

                var template = new Template<Transaction, BsonDocument>(@"[
                            {
                                $match: { <Enabled>: true }
                            },
                            {
                                $group: {
                                    _id: { Type: '$Type', Date: { $dateToString: { date: '$<CreatedOn>', format: '<periodFormat>'} } },
                                    Amount: {
                                        $sum: '$Amount'
                                    }
                                }
                            },
                            {
                                $group: {
                                    _id: '$_id.Date',
                                    Discount: {
                                        $sum: {
                                            $cond: [{
                                                $eq: ['$_id.Type', 'Discount']
                                            }, { $toDouble: '$Amount' }, { $toDouble: 0 }]
                                        }
                                    },
                                    Debt: {
                                        $sum: {
                                            $cond: [{
                                                $eq: ['$_id.Type', 'Debt']
                                            }, { $toDouble: '$Amount' }, { $toDouble: 0 }]
                                        }
                                    },
                                    Paid: {
                                        $sum: {
                                            $cond: [{
                                                $eq: ['$_id.Type', 'Payment']
                                            }, { $toDouble: '$Amount' }, { $toDouble: 0 }]
                                        }
                                    }
                                }
                            },
                        ]")
                    .Path(x => x.CreatedOn)
                    .Path(x => x.Enabled)
                    .Tag("periodFormat", periodFormat);

                var pipeline = await DB.PipelineAsync(template);

                var formationCount = await DB.Fluent<Invoice>()
                    .Match(new BsonDocument("$expr", new BsonDocument("$eq", new BsonArray { "$Enabled", true })))
                    .Group(new BsonDocument
                    {
                        {
                            "_id", new BsonDocument("$dateToString", new BsonDocument
                            {
                                { "date", "$CreatedOn" },
                                { "format", periodFormat }
                            })
                        },
                        { "Count", new BsonDocument("$sum", 1) }
                    })
                    .ToListAsync();

                var result = pipeline
                    .Join(
                        formationCount,
                        payment => payment["_id"],
                        formation => formation["_id"],
                        (payment, formation) => new
                        {
                            Date = payment["_id"].AsString,
                            Discount = payment["Discount"].AsDouble,
                            Debt = payment["Debt"].AsDouble,
                            Paid = payment["Paid"].AsDouble,
                            Count = formation["Count"].AsInt32,
                        });

                return Ok(new StatsResultResponse<IEnumerable<object>, object>(
                    //pipeline.Select(x => BsonSerializer.Deserialize<object>(x)),
                    result,
                    new
                    {
                        Discount = pipeline.Sum(x => x["Discount"].AsDouble),
                        Debt = pipeline.Sum(x => x["Debt"].AsDouble),
                        Paid = pipeline.Sum(x => x["Paid"].AsDouble)
                    },
                    $"All Time"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }
    }
}