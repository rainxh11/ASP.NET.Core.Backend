namespace MegaDatabaseBackup;

public class BackupOptions
{
    public bool Background { get; set; } = false;

    public string WorkingDir { get; set; }

    public string DbName { get; set; }

    public string BackupName { get; set; }
    public string Host { get; set; }

    public string Email { get; set; }
    public string Password { get; set; }
}