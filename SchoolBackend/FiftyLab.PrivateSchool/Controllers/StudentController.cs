using System.Linq.Dynamic.Core;
using FiftyLab.PrivateSchool.Helpers;
using FiftyLab.PrivateSchool.Hubs;
using FiftyLab.PrivateSchool.Models.Request;
using FiftyLab.PrivateSchool.Response;
using FiftyLab.PrivateSchool.Services;
using Hangfire;
using Jetsons.JetPack;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Entities;
using Extensions = MongoDB.Entities.Extensions;

namespace FiftyLab.PrivateSchool.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class StudentController : ControllerBase
    {
        private readonly ILogger<StudentController> _logger;
        private readonly IHubContext<PrivateSchoolHub> _mailHub;
        private IBackgroundJobClient _backgroundJobs;
        private readonly IConfiguration _config;
        private readonly IIdentityService _identityService;
        private readonly LoginInfoSaver _loginSaver;
        private NotificationService _notificationService;
        private readonly ITokenService _tokenService;

        public StudentController(
            ILogger<StudentController> logger,
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

        //--------------------------------------------------------------------------------------------------------------//

        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpGet]
        [Route("{id}")]
        public async Task<IActionResult> GetStudent(string id, CancellationToken ct)
        {
            try
            {
                var student = await DB.Find<Student>().MatchID(id).ExecuteAsync(ct);

                return Ok(student);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        //--------------------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpGet]
        [EnableQuery(AllowedQueryOptions = AllowedQueryOptions.All)]
        [Route("odata")]
        public async Task<IActionResult> GetStudentsOdata(CancellationToken ct)
        {
            try
            {
                var students = await DB.Find<Student>().ExecuteAsync(ct);
                return Ok(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetStudents(int page = 1, int limit = 10, string? sort = "CreatedOn",
            bool desc = true)
        {
            try
            {
                var students = await DB.PagedSearch<Student>()
                    .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                    .PageNumber(page)
                    .PageSize(limit)
                    .ExecuteAsync();

                return Ok(new PagedResultResponse<IEnumerable<Student>>(
                    students.Results,
                    students.TotalCount,
                    students.PageCount,
                    limit,
                    page));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        //--------------------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpGet]
        [Route("debt")]
        //[ResponseCache(VaryByQueryKeys = new string[] { "page", "limit", "sort", "desc" }, Duration = 240)]
        public async Task<IActionResult> GetDebtStudents(CancellationToken ct, int page = 1,
            int limit = -1, string? sort = "Debt", bool desc = true)
        {
            try
            {
                var fluent = await DB.Fluent<Invoice>()
                    .Match(f => f.Eq(x => x.Type, InvoiceType.Debt))
                    //.Group(x => x.Student.ID, x => x)
                    .ToListAsync(ct);

                var grouped = fluent.GroupBy(x => x.Student.ID, x => x).ToList();

                var debtStudents = grouped
                    .Select(x => new StudentDebt(x.First().Student, x.Sum(z => z.LeftUnpaid)))
                    .AsQueryable()
                    .OrderBy(sort, desc ? "DESC" : "ASC")
                    .Skip(page <= 0 ? 0 : page - 1)
                    .Take(limit <= 0 ? int.MaxValue : limit)
                    .ToList();
                var total = grouped.Sum(x => x.Sum(x => x.LeftUnpaid));

                return Ok(new DebtPagedResultResponse<IEnumerable<object>>(
                    debtStudents,
                    grouped.Count,
                    page,
                    limit,
                    page,
                    total
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        //--------------------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpGet]
        [Route("search")]
        public async Task<IActionResult> SearchStudents(string search = "", int page = 1, int limit = 10,
            string? sort = "CreatedOn", bool desc = true, bool lookup = false)
        {
            try
            {
                search = search == "|" ? "" : search;
                search = $"/{search}/i";
                var searchQuery = Extensions.Match(DB.Fluent<Student>(), student => student.Regex(x => x.Name, search) |
                    student.Regex(x => x.Description, search) |
                    student.Regex(x => x.CreatedBy.Name, search) |
                    student.Regex(x => x.CreatedBy.Description, search) |
                    student.Regex(x => x.CreatedBy.UserName, search) |
                    student.Regex(x => x.PhoneNumber, search) |
                    student.Regex(x => x.Address, search) |
                    student.ElemMatch(x => x.Parents,
                        new FilterDefinitionBuilder<Parent>().Regex(p => p.Name, search)) |
                    student.ElemMatch(x => x.Parents,
                        new FilterDefinitionBuilder<Parent>().Regex(p => p.Address, search)) |
                    student.ElemMatch(x => x.Parents,
                        new FilterDefinitionBuilder<Parent>().Regex(p => p.CardID, search)) |
                    student.ElemMatch(x => x.Parents,
                        new FilterDefinitionBuilder<Parent>().Regex(p => p.PhoneNumber, search))
                );

                var students = await DB.PagedSearch<Student>()
                    .WithFluent(searchQuery)
                    .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                    .PageNumber(lookup ? 1 : page)
                    .PageSize(lookup ? int.MaxValue : limit)
                    .ExecuteAsync();

                if (!lookup)
                {
                    return Ok(new PagedResultResponse<IEnumerable<Student>>(
                        students.Results,
                        students.TotalCount,
                        students.PageCount,
                        limit,
                        page));
                }
                else
                {
                    return Ok(new ResultResponse<IEnumerable<object>, string>(students.Results.Select(x => new
                    {
                        x.ID,
                        x.Name,
                        x.PhoneNumber,
                        x.DateOfBirth
                    }), "Success"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        //--------------------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpGet]
        [Route("debt/search")]
        [ResponseCache(VaryByQueryKeys = new[] { "page", "limit", "sort", "desc", "search" }, Duration = 240)]
        public async Task<IActionResult> SearchDebtStudents(CancellationToken ct, string? search = "", int page = 1,
            int limit = -1, string? sort = "Debt", bool desc = true)
        {
            try
            {
                search = search == "|" ? "" : search;
                search = $"/{search}/i";
                var fluent = await DB.Fluent<Invoice>()
                    .Match(f => f.Eq(x => x.Type, InvoiceType.Debt) &
                                (f.Regex(x => x.Student.Name, search) | f.Regex(x => x.Student.ID, search)))
                    //.Group(x => x.Student.ID, x => x)
                    .ToListAsync(ct);

                var grouped = fluent.GroupBy(x => x.Student.ID, x => x).ToList();


                var debtStudents = grouped
                    .Select(x => new StudentDebt(x.First().Student, x.Sum(z => z.LeftUnpaid)))
                    .AsQueryable()
                    .OrderBy(sort, desc ? "DESC" : "ASC")
                    .Skip(page <= 0 ? 0 : page - 1)
                    .Take(limit <= 0 ? int.MaxValue : limit)
                    .ToList();

                var total = grouped.Sum(x => x.Sum(x => x.LeftUnpaid));

                return Ok(new DebtPagedResultResponse<IEnumerable<object>>(
                    debtStudents,
                    grouped.Count,
                    page,
                    limit,
                    page,
                    total
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        //--------------------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpPost]
        [Route("")]
        public async Task<IActionResult> CreateStudent([FromBody] StudentModel model)
        {
            try
            {
                var account = await _identityService.GetCurrentAccount(HttpContext);

                var student = model.Adapt<Student>();
                student.Name = student.Name.ToUpperInvariant();
                student.CreatedBy = account.ToBaseAccount();
                await student.InsertAsync();

                return Ok(new ResultResponse<Student, string>(student, "Student Created!"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        //--------------------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpPatch]
        [Route("{id}")]
        public async Task<IActionResult> UpdateStudent(string id, [FromBody] StudentUpdateModel model,
            CancellationToken ct)
        {
            var transaction = DB.Transaction();
            try
            {
                var bson = model.ToBsonDocument();
                var pipeline = new EmptyPipelineDefinition<BsonDocument>()
                    .AppendStage($"{{ $set : {bson} }}", BsonDocumentSerializer.Instance);

                var update = await DB.Database("CrecheDb")
                    .GetCollection<BsonDocument>(nameof(Student))
                    .UpdateOneAsync(transaction.Session,
                        new { _id = ObjectId.Parse(id) }.ToBsonDocument(),
                        pipeline,
                        cancellationToken: ct);

                var student = await DB.Find<Student>(transaction.Session)
                    .MatchID(id)
                    .ExecuteSingleAsync(ct);
                await transaction.CommitAsync();

                return Ok(new ResultResponse<Student, string>(student, "Student Updated!"));
            }
            catch (Exception ex)
            {
                if (transaction.Session.IsInTransaction)
                    await transaction.AbortAsync();
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        //--------------------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpDelete]
        [Route("{id}")]
        public async Task<IActionResult> DeleteStudent(string id)
        {
            try
            {
                var delete = await DB.Collection<Student>()
                    .DeleteOneAsync(x => x.ID == id);
                if (!delete.IsAcknowledged)
                    return BadRequest(new MessageResponse<DeleteResult>(delete));

                return Ok(new MessageResponse<DeleteResult>(delete));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        //--------------------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpGet]
        [Route("{id}/invoice")]
        public async Task<IActionResult> GetStudentInvoices(string id, CancellationToken ct, int page = 1,
            int limit = -1, string? sort = "CreatedOn", bool desc = true)
        {
            try
            {
                var invoices = await DB.PagedSearch<Invoice>()
                    .Match(x => x.Student.ID == id)
                    .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                    .PageNumber(page)
                    .PageSize(limit < 0 ? Int32.MaxValue : limit)
                    .ExecuteAsync(ct);

                return Ok(new PagedResultResponse<IEnumerable<Invoice>>(
                    invoices.Results,
                    invoices.TotalCount,
                    invoices.PageCount,
                    limit,
                    page));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        //--------------------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpGet]
        [Route("{id}/debt")]
        public async Task<IActionResult> GetStudentDebt(string id, CancellationToken ct)
        {
            try
            {
                var invoices = await DB.Find<Invoice>()
                    .Match(x => x.Student.ID == id && x.Enabled)
                    .ExecuteAsync(ct);

                var debt = invoices.Sum(x => x.LeftUnpaid);
                var unpaidInvoices = invoices.Where(x => x.LeftUnpaid > 0).ToList();

                var from = unpaidInvoices
                    .OrderBy(x => x.Transactions
                        .Where(z => z.Type == TransactionType.Debt)
                        .OrderBy(z => z.CreatedOn)
                        .First()
                        .CreatedOn
                    );
                var to = unpaidInvoices
                    .OrderByDescending(x => x.Transactions
                        .Where(z => z.Type == TransactionType.Debt)
                        .OrderBy(z => z.CreatedOn)
                        .First()
                        .CreatedOn
                    );


                return Ok(new { TotalDebt = debt, From = from, To = to });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpPost]
        [Route("{id}/debt")]
        public async Task<IActionResult> PayStudentDebt(string id, [FromBody] PaymentModel model, CancellationToken ct)
        {
            var transaction = DB.Transaction();
            try
            {
                var account = await _identityService.GetCurrentAccount(HttpContext);
                var invoices = await DB.Find<Invoice>(transaction.Session)
                    .Match(x => x.Student.ID == id && x.Enabled)
                    .ExecuteAsync(ct);

                var totalDebt = invoices.Sum(x => x.LeftUnpaid);
                if (totalDebt <= 0)
                    return BadRequest(new { Message = "Student has 0 debt!" });

                model.Paid = model.Paid.ToDouble(0) > totalDebt
                    ? totalDebt
                    : model.Paid.ToDouble(0);

                model.Discount = model.Discount.ToDouble(0) > (totalDebt - model.Paid.ToDouble(0))
                    ? (totalDebt - model.Paid.ToDouble(0))
                    : model.Discount.ToDouble(0);

                var invoiceCount = invoices.Count(x => x.LeftUnpaid > 0);


                foreach (var invoice in invoices.Where(x => x.LeftUnpaid > 0))
                {
                    var discount = model.Discount >= invoice.LeftUnpaid ? invoice.LeftUnpaid : model.Discount;
                    var paid = model.Paid >= (invoice.LeftUnpaid - discount)
                        ? (invoice.LeftUnpaid - discount)
                        : model.Paid;

                    var transactions = Enumerable
                        .Empty<Transaction>()
                        .Append(new Transaction(TransactionType.Payment, paid.ToDouble(0),
                            account.ToBaseAccount()))
                        .Append(new Transaction(TransactionType.Discount, discount.ToDouble(0),
                            account.ToBaseAccount()))
                        .Where(x => x.Amount > 0)
                        .ToList();

                    var leftUnpaid = invoice.LeftUnpaid - transactions
                        .Where(x => x.Enabled && x.Type == TransactionType.Payment)
                        .Sum(x => x.Amount);

                    if (transactions.Count == 0)
                    {
                        invoice.Type = invoice.LeftUnpaid > 0 ? InvoiceType.Debt : InvoiceType.Paid;
                        await invoice.SaveAsync(transaction.Session, ct);
                    }
                    else
                    {
                        await transactions.SaveAsync(transaction.Session, ct);
                        await invoice.Transactions.AddAsync(transactions, transaction.Session, ct);


                        invoice.Type = leftUnpaid > 0 ? InvoiceType.Debt : InvoiceType.Paid;

                        await invoice.SaveAsync(transaction.Session, ct);
                    }
                }

                await transaction.CommitAsync(ct);
                return Ok();
            }
            catch (Exception ex)
            {
                if (transaction.Session.IsInTransaction)
                    await transaction.AbortAsync();
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        //--------------------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpGet]
        [Route("{id}/invoice/search")]
        public async Task<IActionResult> SearchStudentInvoices(string id, CancellationToken ct, string search = "",
            int page = 1, int limit = -1, string? sort = "CreatedOn", bool desc = true)
        {
            try
            {
                search = $"/{search}/ig";
                var searchQuery = DB.Fluent<Invoice>()
                    .Match(x => x.Student.ID == id)
                    .Match(formation => formation.Regex(x => x.Student.ID, search) |
                                        formation.Regex(x => x.Student.Name, search) |
                                        formation.Regex(x => x.Formation.Name, search) |
                                        formation.Regex(x => x.Formation.ID, search) |
                                        formation.Regex(x => x.Formation.Price, search) |
                                        formation.Regex(x => x.CreatedBy.Description, search) |
                                        formation.Regex(x => x.CreatedBy.Name, search) |
                                        formation.Regex(x => x.CreatedBy.UserName, search) |
                                        formation.Regex(x => x.CreatedBy.ID, search));


                var invoices = await DB.PagedSearch<Invoice>()
                    .WithFluent(searchQuery)
                    .Sort(s => desc ? s.Descending(sort) : s.Ascending(sort))
                    .PageNumber(page)
                    .PageSize(limit < 0 ? Int32.MaxValue : limit)
                    .ExecuteAsync(ct);

                return Ok(new PagedResultResponse<IEnumerable<Invoice>>(
                    invoices.Results,
                    invoices.TotalCount,
                    invoices.PageCount,
                    limit,
                    page));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }

        //--------------------------------------------------------------------------------------------------------------//
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User},{AccountRole.Secretary}")]
        [HttpGet]
        [Route("parent")]
        public async Task<IActionResult> SearchParents(CancellationToken ct, string? search)
        {
            try
            {
                var searchRegex = search is null ? "|" : $"/{search}/ig";
                var parents = await DB.Fluent<Student>()
                    .Match(student =>
                        student.ElemMatch(x => x.Parents,
                            new FilterDefinitionBuilder<Parent>().Regex(p => p.Name, searchRegex)) |
                        student.ElemMatch(x => x.Parents,
                            new FilterDefinitionBuilder<Parent>().Regex(p => p.Address, searchRegex)) |
                        student.ElemMatch(x => x.Parents,
                            new FilterDefinitionBuilder<Parent>().Regex(p => p.CardID, searchRegex)) |
                        student.ElemMatch(x => x.Parents,
                            new FilterDefinitionBuilder<Parent>().Regex(p => p.PhoneNumber, searchRegex)))
                    .ToListAsync(ct);

                var result = parents
                    .SelectMany(x => x.Parents)
                    .Distinct();

                return Ok(new ResultResponse<IEnumerable<Parent>, int>(
                    result,
                    result.Count()
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest();
            }
        }
    }

    public record StudentDebt(StudentBase Student, double Debt);
}