using CG.Web.MegaApiClient;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MegaDatabaseBackup
{
    internal class App
    {
        public static async Task UploadFile(string email, string password, string backupName, string fileName, Progress<double> progress)
        {
            try
            {
                MegaApiClient client = new MegaApiClient();
                client.Login(email, password);

                var file = new FileInfo(fileName);

                var nodes = client.GetNodes();
                var rootNode = nodes.ToList().Find(x => x.Type == NodeType.Root);
                var backupNode = nodes.ToList().Find(x => x.Name == backupName);

                try
                {
                    var existingNodes = nodes.Where(x => x.Name == file.Name.Replace(".zpaq", "")).OrderByDescending(x => x.CreationDate).ToList();

                    existingNodes.Skip(1).ToList().ForEach(x =>
                    {
                        client.Delete(x, false);
                    });
                }
                catch
                {
                }

                if (backupNode == null)
                {
                    backupNode = client.CreateFolder(backupName, rootNode);
                }

                await client.UploadFileAsync(fileName, backupNode, progress);

                client.Logout();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void UploadFileSync(string email, string password, string backupName, string fileName)
        {
            try
            {
                MegaApiClient client = new MegaApiClient();
                client.Login(email, password);

                var nodes = client.GetNodes();
                var rootNode = nodes.ToList().Find(x => x.Type == NodeType.Root);
                var backupNode = nodes.ToList().Find(x => x.Name == backupName);
                if (backupNode == null)
                {
                    backupNode = client.CreateFolder(backupName, rootNode);
                }
                var existing = client.GetNodes(backupNode);

                if (existing.Any(x => x.Name.Contains(fileName)))
                {
                    client.Delete(existing.Where(x => x.Name.Contains(fileName)).FirstOrDefault());
                }
                client.UploadFile(fileName, backupNode);

                client.Logout();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void CreateBackup(string dbName, string destinationFile, string host, string workingDir)
        {
            Process mongodump = new Process()
            {
                StartInfo =
                {
                    FileName = @"mongodump.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    Arguments = $"/host:{host} /db:{dbName} /archive:{destinationFile} /gzip"
                }
            };
            try
            {
                mongodump.Start();
                mongodump.WaitForExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void CreateBackupZPAQ(string dbName, string destinationFile, string host, string workingDir, int compression = 5)
        {
            Process mongodump = new Process()
            {
                StartInfo =
                {
                    FileName = @"mongodump.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    Arguments = $"/host:{host} /db:{dbName}"
                }
            };
            Process zpaq = new Process()
            {
                StartInfo =
                {
                    FileName = "zpaq.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    Arguments = $"a {destinationFile} .\\dump\\ -m{compression}"
                }
            };
            try
            {
                mongodump.Start();
                mongodump.WaitForExit();
                zpaq.Start();
                zpaq.WaitForExit();
                Directory.Delete(Directory.GetCurrentDirectory() + @"\dump", true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}