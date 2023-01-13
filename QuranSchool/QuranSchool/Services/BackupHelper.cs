using MegaDatabaseBackup;

namespace QuranSchool.Services;

public class BackupHelper
{
    private readonly IConfiguration _config;
    private readonly ILogger<BackupHelper> _logger;

    public BackupHelper(IConfiguration config, ILogger<BackupHelper> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task PerformBackup()
    {
        try
        {
            var backupOptions = new BackupOptions
            {
                DbName = _config["MongoDB:DatabaseName"],
                WorkingDir = _config["Backup:Path"],
                Email = _config["Backup:Email"],
                Password = _config["Backup:Password"],
                BackupName = _config["Backup:Name"],
                Host = _config["Backup:Host"],
                Background = true
            };

            switch (_config["Backup:Provider"])
            {
                default:
                case "Mega.nz":
                    await MegaBackup.RunBackup(backupOptions);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }
    }
}