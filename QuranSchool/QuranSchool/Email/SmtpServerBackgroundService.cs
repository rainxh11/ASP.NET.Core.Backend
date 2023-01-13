namespace QuranSchool.Email;

public class SmtpServerBackgroundService : BackgroundService
{
    private readonly SmtpServer.SmtpServer _smtpServer;
    private readonly ILogger<SmtpServerBackgroundService> _logger;

    public SmtpServerBackgroundService(ILogger<SmtpServerBackgroundService> logger, SmtpServer.SmtpServer smtpServer)
    {
        _logger = logger;
        _smtpServer = smtpServer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("[EMAIL SERVER] Email Server {0}", "Started");
        await _smtpServer.StartAsync(stoppingToken);
    }
}