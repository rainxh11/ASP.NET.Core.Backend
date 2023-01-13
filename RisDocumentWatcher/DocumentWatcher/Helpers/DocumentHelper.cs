using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.CustomProperties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocumentWatcher.Models;
using System.Text.RegularExpressions;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using MongoDB.Entities;
using Newtonsoft.Json.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;
using Xceed.Words.NET;
using Xceed.Document.NET;

namespace DocumentWatcher.Helpers
{
    public class DocumentHelper
    {
        public static void OpenDocument(string file)
        {
            try
            {
                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "cmd.exe",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden,

                        Arguments = $"/c \"{file}\" &"
                    }
                };
                process.Start();
            }
            catch
            {
            }
        }

        public static string CreateNewStudyFile(RisStudy study)
        {
            var config = ConfigHelper.GetConfig();

            var client = DB.Find<RisClient>().MatchID(study.client.ToString()).ExecuteSingleAsync().GetAwaiter().GetResult();

            var filename = $"{config.DocumentFolder}\\{study._id}_{client.NameWithAge}.temp";

            DocumentHelper.CreateDocument(filename, study, client);

            DB.Update<RisStudy>()
                .Match(x => x._id == study._id && (x.statusStudy == "new" || x.statusStudy == "complete" || x.statusStudy == "inProgress"))
                .Modify(x => x.statusStudy, "inProgress")
                .ExecuteAsync()
                .GetAwaiter()
                .GetResult();

            return filename;
        }

        public static void CreateDocument(string filename, RisStudy study, RisClient client)
        {
            try
            {

                File.Copy(AppContext.BaseDirectory + @"\Document\Document.docx", filename, true);

                using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(filename, true, new OpenSettings()
                {
                    AutoSave = true,
                   
                }))
                {
                    var parts = wordDoc.Parts;
                    using (StreamReader sr = new StreamReader(wordDoc.MainDocumentPart.GetStream(FileMode.Open)))
                    {
                        var docText = sr.ReadToEnd();

                        docText = docText
                       .Replace("#DICOMNAME", $"{client.NameWithAge}")
                       .Replace("#NAME", $"{client.Name}")
                       .Replace("#DC", $"{study.doctor.ToUpper().Trim()}")
                       .Replace("#AGE", $"{client.Age}")
                       .Replace("#TITLE", $"{study.examType.ToUpper().Trim()}")
                       .Replace("#DATE", $"{DateTime.Now.ToString("dd MMMM yyyy", CultureInfo.CreateSpecificCulture("fr-FR")).ToUpperInvariant()}");

                        sr.Close();

                        using (StreamWriter sw = new StreamWriter(wordDoc.MainDocumentPart.GetStream(FileMode.Create)))
                        {
                            sw.Write(docText);
                            sw.Close();
                        }
                    }
                    wordDoc.PackageProperties.Language = "fr-FR";
                    wordDoc.PackageProperties.Creator = "50LAB RIS - Dr.LAGHOUATI";
                    wordDoc.PackageProperties.ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                    wordDoc.PackageProperties.Created = DateTime.Now;
                    wordDoc.PackageProperties.Description = $"Patient: {client.NameWithAge}, Examen: {study.modality} - {study.examType}";
                    wordDoc.PackageProperties.Identifier = $"RIS_StudyId_{study._id}";
                    wordDoc.PackageProperties.Subject = $"{client.NameWithAge}";
                    wordDoc.PackageProperties.Title = $"Compte-Rendu de {study.modality} {study.examType}";

                    wordDoc.Save();
                    wordDoc.Close();
                }

                File.Move(filename, filename.Replace(".temp", ".docx"), true);
                var document = Xceed.Words.NET.DocX.Load(filename.Replace(".temp", ".docx"));
                document.AddCoreProperty("dc:language", "fr-FR");
                document.AddCoreProperty("language", "fr-FR");
                document.Save();
                File.SetCreationTime(filename.Replace(".temp", ".docx"), study.createdAt);

                var reportSync = new ReportSync()
                {
                    date = study.createdAt.ToString("yyyy-MM-dd"),
                    doctor = study.doctor.ToUpper(),
                    age = client.Age,
                    familyName = client.familyName.ToUpper(),
                    title = study.examType
                };

                Task.WaitAny(
                    DB.Update<RisStudy>()
                    .Match(x => x._id == study._id)
                    .Modify(x => x.reportSync, reportSync)
                    .ExecuteAsync());
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.Message);
            }
        }

        public static async Task ToggleStudy(FileInfo file, string status)
        {
            try
            {
                string id = file.Name.Split('_').First();

                await DB.Update<RisStudy>()
                    .Match(x => x._id == Convert.ToInt32(id) && (x.statusStudy == "complete" || x.statusStudy == "inProgress"))
                    .Modify(x => x.statusStudy, status)
                    .ExecuteAsync();
            }
            catch (Exception ex)
            {
               //Console.WriteLine($"Error in saving status of file : '{file.Name}', Exception: {Environment.NewLine}{ex.Message}");
            }
        }

        public static async Task CatchupStudy(FileInfo file)
        {
            try
            {
                string id = file.Name.Split('_').First();
                var newStudy = await DB.Find<RisStudy>().Match(x => x.statusStudy == "new" && x._id == Convert.ToInt32(id)).ExecuteAnyAsync();
                if (newStudy)
                {
                    var docJson = await Program.documentServerApi.UploadDocument(file);

                    dynamic data = JObject.Parse(docJson);
                    dynamic blocks = data.sections[0].blocks;

                    var valueBlocks = blocks as JArray;
                    var blockBson = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonArray>(valueBlocks.ToString());
                    blockBson = new BsonArray(blockBson.Skip(6));

                    await DB.Update<RisStudy>()
                        .Match(x => x._id == Convert.ToInt32(id))
                        .Modify(x => x.reportSync.block, blockBson)
                        .ExecuteAsync();

                    await  DB.Update<RisStudy>()
                        .Match(x => x._id == Convert.ToInt32(id) &&  x.statusStudy != "delivered")
                        .Modify(x => x.statusStudy, "complete")
                        .ExecuteAsync();         

                }
            }
            catch (Exception ex)
            {
               //Console.WriteLine(ex.Message);
            }
        }

        public static async Task<bool> UpdateStudy(FileInfo file)
        {
            try
            {
                var docJson = await Program.documentServerApi.UploadDocument(file);
                string id = file.Name.Split('_').First();
                dynamic data = JObject.Parse(docJson);
                dynamic blocks = data.sections[0].blocks;

                var valueBlocks = blocks as JArray;
                var blockBson = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonArray>(valueBlocks.ToString());
                blockBson = new BsonArray(blockBson.Skip(6));

                await DB.Update<RisStudy>()
                    .Match(x => x._id == Convert.ToInt32(id))
                    .Modify(x => x.statusStudy, "complete")
                    .Modify(x => x.reportSync.block, blockBson)
                    .ExecuteAsync();
                return true;
            }
            catch (Exception ex)
            {
               //Console.WriteLine(ex.Message);
               return false;
            }
        }
    }
}