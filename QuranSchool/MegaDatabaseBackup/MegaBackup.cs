using ShellProgressBar;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MegaDatabaseBackup
{
    internal static class Utils
    {
        public static string MakeFileSystemSafe(this string s)
        {
            return new string(s.Where(IsFileSystemSafe).ToArray());
        }

        public static bool IsFileSystemSafe(char c)
        {
            return !Path.GetInvalidFileNameChars().Contains(c);
        }
    }

    public class MegaBackup
    {
        public static async Task RunBackup(BackupOptions opts)
        {
            var backupDir = new DirectoryInfo(opts.WorkingDir).FullName;
            var outputFileZpaq = Path.Combine(backupDir,
                Utils.MakeFileSystemSafe(
                    $"{opts.BackupName}_{Environment.MachineName}_{DateTime.Now.ToString("yyyy-MM")}.zpaq"));

            try
            {
                new FileInfo(outputFileZpaq).Directory.Create();
            }
            catch
            {
            }


            App.CreateBackupZPAQ(opts.DbName, outputFileZpaq, opts.Host, opts.WorkingDir);

            /*ProgressBar progressBar = new ProgressBar(10000, $"Uploading Database Backup: {outputFileZpaq}",
                new ProgressBarOptions()
                {
                    ProgressBarOnBottom = true,
                    DisplayTimeInRealTime = true,
                });
            IProgress<double> progress = progressBar.AsProgress<double>();

            Progress<double> uploadProgress = new Progress<double>((x) => { progress.Report(x / 100); });*/
            await Task.Run(() => { App.UploadFileSync(opts.Email, opts.Password, opts.BackupName, outputFileZpaq); });

            /*if (opts.Background)
            {
                App.UploadFileSync(opts.Email, opts.Password, opts.BackupName, outputFileZpaq);
            }
            else
            {
                await App.UploadFile(opts.Email, opts.Password, opts.BackupName, outputFileZpaq, uploadProgress);
            }*/
        }
    }
}