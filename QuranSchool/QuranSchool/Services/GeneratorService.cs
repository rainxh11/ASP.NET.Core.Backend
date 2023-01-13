using Microsoft.AspNetCore.Identity;
using MongoDB.Entities;
using QuranSchool.Models;

namespace QuranSchool.Services;

public class GeneratorService : BackgroundService
{
    private readonly AccountGenerator _generator;
    private readonly ILogger<GeneratorService> _logger;
    private readonly UserManager<Account> _userManager;
    private readonly IConfiguration _configuration;

    public GeneratorService(IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<GeneratorService> logger)
    {
        var scope = scopeFactory.CreateScope();
        _configuration = configuration;
        _userManager = scope.ServiceProvider.GetService<UserManager<Account>>();

        _generator = scope.ServiceProvider.GetService<AccountGenerator>();
        _logger = logger;
    }

    private async Task CreateDefaultAdminAccount()
    {
        try
        {
            var adminExist = await DB.Find<Account>().Match(x => x.UserName == "admin" && x.Enabled).ExecuteAnyAsync();
            if (!adminExist)
            {
                var account = new Account
                {
                    Name = "Administrator",
                    Description = "Default Administrator",
                    UserName = "admin",
                    Role = AccountType.Admin,
                    Email = "admin@madrasacloud.com",
                    EmailConfirmed = true
                };

                await _userManager.CreateAsync(account, _configuration["DefaultAdminPassword"]);
                //var token = _userManager.GenerateEmailConfirmationTokenAsync(account);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public async Task GenerateStudents(CancellationToken stoppingToken)
    {
        try
        {
            var students = await DB.Find<Student>()
                .ExecuteAsync(stoppingToken);

            var accounts = await DB.Find<Account>()
                .Match(f => f.Eq(x => x.Role, AccountType.Student) &
                            f.In(x => x.PersonalId, students.Select(x => x.ID)))
                .ExecuteAsync(stoppingToken);
            var accountIds = accounts.Select(x => x.PersonalId);

            var notExist = students.Where(x => !accountIds.Contains(x.ID));

            foreach (var student in notExist)
            {
                await _generator.CreateAccount(student.ID,
                    new Models.Request.CreateAccountModel()
                    {
                        Name = student.Name,
                        Description = student.Description,
                        Role = AccountType.Student
                    });

                _logger.LogInformation($"'{student.Name}' Account Generated");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }
    }

    public async Task GenerateTeachers(CancellationToken stoppingToken)
    {
        try
        {
            var teachers = await DB.Find<Teacher>()
                .ExecuteAsync(stoppingToken);

            var accounts = await DB.Find<Account>()
                .Match(f => f.Eq(x => x.Role, AccountType.Teacher) &
                            f.In(x => x.PersonalId, teachers.Select(x => x.ID)))
                .ExecuteAsync(stoppingToken);
            var accountIds = accounts.Select(x => x.PersonalId);

            var notExist = teachers.Where(x => !accountIds.Contains(x.ID));

            foreach (var teacher in notExist)
            {
                await _generator.CreateAccount(teacher.ID,
                    new Models.Request.CreateAccountModel()
                    {
                        Name = teacher.Name,
                        Description = teacher.Description,
                        Role = AccountType.Teacher
                    });

                _logger.LogInformation($"'{teacher.Name}' Account Generated");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CreateDefaultAdminAccount();

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                await GenerateStudents(stoppingToken);
                await GenerateTeachers(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            finally
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
    }
}