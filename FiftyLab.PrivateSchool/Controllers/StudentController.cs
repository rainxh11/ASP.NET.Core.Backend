using FiftyLab.PrivateSchool.Helpers;
using FiftyLab.PrivateSchool.Hubs;
using FiftyLab.PrivateSchool.Models.Request;
using FiftyLab.PrivateSchool.Response;
using FiftyLab.PrivateSchool.Services;
using Hangfire;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
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
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetStudents(int page = 1, int limit = 10, string? sort = "CreatedOn", bool desc = true)
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
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
        [HttpGet]
        [Route("search")]
        public async Task<IActionResult> SearchStudents(string search = "", int page = 1, int limit = 10, string? sort = "CreatedOn", bool desc = true, bool lookup = false)
        {
            try
            {

                search = $"/{search}/ig";
                var searchQuery = Extensions.Match(DB.Fluent<Student>(), student => student.Regex(x => x.Name, search) |
                                                            student.Regex(x => x.Description, search) |
                                                            student.Regex(x => x.CreatedBy.Name, search) |
                                                            student.Regex(x => x.CreatedBy.Description, search) |
                                                            student.Regex(x => x.CreatedBy.UserName, search) |
                                                            student.Regex(x => x.PhoneNumber, search) |
                                                            student.Regex(x => x.Address, search) |
                                                            student.ElemMatch(x => x.Parents, new FilterDefinitionBuilder<Parent>().Regex(p => p.Name, search)) |
                                                            student.ElemMatch(x => x.Parents, new FilterDefinitionBuilder<Parent>().Regex(p => p.Address, search)) |
                                                            student.ElemMatch(x => x.Parents, new FilterDefinitionBuilder<Parent>().Regex(p => p.CardID, search)) |
                                                            student.ElemMatch(x => x.Parents, new FilterDefinitionBuilder<Parent>().Regex(p => p.PhoneNumber, search))
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
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
        [HttpPost]
        [Route("")]
        public async Task<IActionResult> CreateStudent([FromBody] StudentModel model)
        {
            try
            {
                var account = await _identityService.GetCurrentAccount(HttpContext);

                var student = model.Adapt<Student>();
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
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
        [HttpPatch]
        [Route("{id}")]
        public async Task<IActionResult> UpdateStudent(string id, [FromBody] StudentUpdateModel model, CancellationToken ct)
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
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
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
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
        [HttpGet]
        [Route("{id}/invoice")]
        public async Task<IActionResult> GetStudentInvoices(string id, CancellationToken ct, int page = 1, int limit = -1, string? sort = "CreatedOn", bool desc = true)
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
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
        [HttpGet]
        [Route("{id}/invoice/search")]
        public async Task<IActionResult> SearchStudentInvoices(string id, CancellationToken ct, string search = "", int page = 1, int limit = -1, string? sort = "CreatedOn", bool desc = true)
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
        [Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
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
}
