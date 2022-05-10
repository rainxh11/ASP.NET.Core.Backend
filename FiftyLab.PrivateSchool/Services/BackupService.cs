using Hangfire;

namespace FiftyLab.PrivateSchool.Services;

public class BackupService : BackgroundService
{
    private BackupHelper _helper;
    private readonly IRecurringJobManager _recurringJobs;
    private readonly IConfiguration _config;


    public BackupService(BackupHelper helper, IRecurringJobManager recurringJobs, IConfiguration config)
    {
        _helper = helper;
        _recurringJobs = recurringJobs;
        _config = config;

    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _recurringJobs.AddOrUpdate("Database Backup",
            () => _helper.PerformBackup(),
            _config["Backup:CronExpression"]);

        return Task.CompletedTask;
    }
}