using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentWatcher.Helpers;
using DocumentWatcher.Models;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using MongoDB.Entities;

namespace DocumentWatcher.Controllers
{
    public class DocumentController : WebApiController
    {
        [Route(HttpVerbs.Post, "/opendocument/{id}")]
        public async Task<string> OpenDocument(int id)
        {
            try
            {
                var config = ConfigHelper.GetConfig();

                var files = new DirectoryInfo(config.DocumentFolder)
                    .GetFiles(config.ExtensionFilter, SearchOption.AllDirectories)
                    .Where(x => x.Name.Split('_')[0] == id.ToString());

                if (files.Count() == 0)
                {
                    var study = await DB.Find<RisStudy>().Match(x => x._id == id).ExecuteSingleAsync();
                    var file = DocumentHelper.CreateNewStudyFile(study);

                    DocumentHelper.OpenDocument(file.Replace(".temp", ".docx"));
                    return file;
                }
                else
                {
                    files.ToList().ForEach(x =>
                    {
                        DocumentHelper.OpenDocument(x.FullName);
                    });
                    return files.Select(x => x.FullName).Aggregate((a, c) => $"{a}{Environment.NewLine}{c}");
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}