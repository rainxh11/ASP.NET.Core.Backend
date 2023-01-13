using Hangfire;

namespace QuranSchool.Services;

public class BackupService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly BackupHelper _helper;
    private readonly IRecurringJobManager _recurringJobs;


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