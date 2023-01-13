using Jetsons.JetPack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Entities;
using QuranSchool.Models;
using QuranSchool.Models.EqualityComparer;
using QuranSchool.Services;
using Transaction = QuranSchool.Models.Transaction;

namespace QuranSchool.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class ImportController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IIdentityService _identityService;
    private readonly ILogger<ImportController> _logger;
    private readonly ExcelConverter _converter;

    public ImportController(
        ExcelConverter converter,
        ILogger<ImportController> logger,
        IIdentityService identityService,
        IConfiguration config)
    {
        _converter = converter;
        _logger = logger;
        _config = config;
        _identityService = identityService;
    }


    [Authorize(Roles = $"{AccountRole.Admin}")]
    //[AllowAnonymous]
    [HttpPost]
    [Route("excel")]
    public async Task<IActionResult> ImportExcel(CancellationToken ct,
        IFormFileCollection files,
        double paid,
        DateTime paymentStartDate)
    {
        var transaction = DB.Transaction();

        try
        {
            var account = await _identityService.GetCurrentAccount(HttpContext);

            var excelStudents = _converter.GetStudents(files.ToList());
            var teacherNames = excelStudents.Select(x => x.TeacherName).Distinct();
            var groupNames = excelStudents.GroupBy(x => $"{x.TeacherName} - حفظ القرآن - GROUPE {x.Group}");

            //---------------------------------------------- TEACHERS
            var teachers = new List<Teacher>();

            foreach (var teacherName in teacherNames)
            {
                Teacher teacher = null;
                if (await DB.Find<Teacher>(transaction.Session)
                        .Match(f => f
                            .Regex(x => x.Name, teacherName))
                        .ExecuteAnyAsync(ct))
                {
                    teacher = await DB.Find<Teacher>(transaction.Session)
                        .Match(f => f
                            .Regex(x => x.Name, teacherName))
                        .ExecuteFirstAsync(ct);
                }
                else
                {
                    teacher = new Teacher()
                    {
                        Name = teacherName
                    };
                    await teacher.InsertAsync(transaction.Session, ct);
                }

                teachers.Add(teacher);
            }

            //------------------------------------- FORMATION
            Formation formation = null;

            if (!await DB.Find<Formation>(transaction.Session)
                    .Match(x => x.Name == "حفظ القرآن")
                    .ExecuteAnyAsync(ct))
            {
                formation = new Formation()
                {
                    DurationDays = 30,
                    Name = "حفظ القرآن",
                    Price = paid
                };
                await formation.InsertAsync(transaction.Session, ct);
            }
            else
            {
                formation = await DB
                    .Find<Formation>(transaction.Session)
                    .Match(x => x.Name == "حفظ القرآن")
                    .ExecuteFirstAsync(ct);
            }

            //--------------------------------------- STUDENTS
            var existingStudents = await DB.Find<Student>(transaction.Session)
                .Match(f => f.In(x => x.Name, excelStudents.Select(x => x.FullName)))
                .ExecuteAsync(ct);
            var newStudents = excelStudents
                .ExceptBy(existingStudents,
                    x => new Student() { Name = x.FullName, DateOfBirth = x.DOB },
                    new StudentEqualityComparer())
                .Select(x => new Student()
                {
                    Name = x.FullName,
                    DateOfBirth = x.DOB,
                    PhoneNumber = x.PhoneNumber,
                    Address = x.Address,
                    Gender = x.Gender,
                    CreatedBy = account.ToBaseAccount(),
                    Description =
                        $"Niveau Scolaire: {x.Level}, Maladie: {x.Sickness}, Lieu de naissance: {x.PlaceOfBirth}",
                    Parents = new List<Parent>()
                    {
                        new()
                        {
                            Name = x.ParentName,
                            Job = x.ParentJob,
                            Address = x.Address
                        }
                    }
                });
            await newStudents.InsertAsync(transaction.Session, ct);
            var students = await DB.Find<Student>(transaction.Session)
                .Match(f => f.In(x => x.Name,
                    excelStudents.Select(x => x.FullName)))
                .ExecuteAsync(ct);

            //------------------------------------------ GROUPS
            var groups = new List<Group>();
            foreach (var groupName in groupNames)
            {
                var names = groupName.Select(x => x.FullName);
                Group group = null;
                if (await DB.Find<Group>(transaction.Session)
                        .Match(x => x.Name == groupName.Key)
                        .ExecuteAnyAsync(ct))
                {
                    group = await DB.Find<Group>(transaction.Session)
                        .Match(x => x.Name == groupName.Key)
                        .ExecuteFirstAsync(ct);
                }
                else
                {
                    var groupTeacher = teachers.First(x => x.Name == groupName.First().TeacherName).ToBase();
                    var groupStudents = students
                        .Where(x => names.Contains(x.Name))
                        .Select(x => x.ToBase())
                        .ToList();
                    group = new Group()
                    {
                        Name = groupName.Key,
                        Teacher = groupTeacher,
                        CreatedBy = account.ToBaseAccount(),
                        Start = paymentStartDate,
                        Formation = formation.ToBase(),
                        Cancelled = false,
                        Students = groupStudents
                    };
                    await group.InsertAsync(transaction.Session, ct);
                }

                groups.Add(group);
            }

            //-------------------------------------------------------------- PAYMENTS
            var payments = students.Select(x => new Invoice()
            {
                InvoiceID = $"{DB.NextSequentialNumberAsync<Invoice>(ct).GetAwaiter().GetResult()}",
                Student = x.ToBase(),
                Formation = formation.ToBase(),
                Type = InvoiceType.Paid,
                StartDate = paymentStartDate
            }).ToList();
            foreach (var invoice in payments)
            {
                var invoiceTransaction = new Transaction(TransactionType.Payment, paid, account.ToBaseAccount());
                await invoiceTransaction.InsertAsync(transaction.Session, ct);
                await invoice.InsertAsync(transaction.Session, ct);
                await invoice.Transactions.AddAsync(invoiceTransaction.ID, transaction.Session, ct);
            }

            await transaction.CommitAsync(ct);

            return Ok(new
            {
                Formation = formation,
                Students = students.Count,
                Invoices = payments.Count,
                Teachers = teachers.Count,
                Groups = groups.Count
            });
        }
        catch (Exception ex)
        {
            if (transaction.Session.IsInTransaction)
                transaction.AbortAsync();

            _logger?.LogError(ex, "");
            throw;
        }
    }
}