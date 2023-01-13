using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.IO;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Syncfusion.EJ2.DocumentEditor;
using WDocument = Syncfusion.DocIO.DLS.WordDocument;
using WFormatType = Syncfusion.DocIO.FormatType;
using Syncfusion.EJ2.SpellChecker;
using RisDocumentServer;
using Dicom.IO;
using Dicom;
using Dicom.Media;
using Syncfusion.DocIO;
using Syncfusion.DocIO.Utilities;
using Syncfusion.DocIORenderer;
using FormatType = Syncfusion.EJ2.DocumentEditor.FormatType;
using Dicom.Network;
using DicomClient = Dicom.Network.Client.DicomClient;
using RisDocumentServer.Helpers.Models;
using System.Net.Mime;
using Syncfusion.DocIO.DLS;
using WordDocument = Syncfusion.EJ2.DocumentEditor.WordDocument;

namespace RisDocumentServer.Controllers
{
    public class DocumentEditorController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private List<DictionaryData> spellDictionary;
        private string personalDictPath;
        private string path;

        public DocumentEditorController(IWebHostEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
            spellDictionary = Startup.spellDictCollection;
            path = Startup.path;
            personalDictPath = Startup.personalDictPath;
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("documentserver/[controller]/Import")]
        public string Import(IFormCollection data)
        {
            if (data.Files.Count == 0)
                return null;
            Stream stream = new MemoryStream();
            IFormFile file = data.Files[0];
            int index = file.FileName.LastIndexOf('.');
            string type = index > -1 && index < file.FileName.Length - 1 ?
                file.FileName.Substring(index) : ".docx";
            file.CopyTo(stream);
            stream.Position = 0;

            Syncfusion.EJ2.DocumentEditor.WordDocument document = Syncfusion.EJ2.DocumentEditor.WordDocument.Load(stream, GetFormatType(type.ToLower()));
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(document);
            document.Dispose();
            return json;
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("documentserver/[controller]/SpellCheck")]
        public string SpellCheck([FromBody] SpellCheckJsonData spellChecker)
        {
            try
            {
                if (spellChecker.LanguageID == 0) spellChecker.LanguageID = 1036;
                SpellChecker spellCheck = new SpellChecker(spellDictionary, personalDictPath);
                spellCheck.GetSuggestions(spellChecker.LanguageID, spellChecker.TexttoCheck, spellChecker.CheckSpelling, spellChecker.CheckSuggestion, spellChecker.AddWord);
                return Newtonsoft.Json.JsonConvert.SerializeObject(spellCheck);
            }
            catch
            {
                return "{\"SpellCollection\":[],\"HasSpellingError\":false,\"Suggestions\":null}";
            }
        }

        // GET api/values
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("documentserver/[controller]/SpellCheckByPage")]
        public string SpellCheckByPage([FromBody] SpellCheckJsonData spellChecker)
        {
            try
            {
                if (spellChecker.LanguageID == 0) spellChecker.LanguageID = 1036;
                SpellChecker spellCheck = new SpellChecker(spellDictionary, personalDictPath);
                spellCheck.CheckSpelling(spellChecker.LanguageID, spellChecker.TexttoCheck);
                return Newtonsoft.Json.JsonConvert.SerializeObject(spellCheck);
            }
            catch
            {
                return "{\"SpellCollection\":[],\"HasSpellingError\":false,\"Suggestions\":null}";
            }
        }

        public class SpellCheckJsonData
        {
            public int LanguageID { get; set; }
            public string TexttoCheck { get; set; }
            public bool CheckSpelling { get; set; }
            public bool CheckSuggestion { get; set; }
            public bool AddWord { get; set; }
        }

        public class CustomParameter
        {
            public string content { get; set; }
            public string type { get; set; }
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("documentserver/[controller]/SystemClipboard")]
        public string SystemClipboard([FromBody] CustomParameter param)
        {
            if (param.content != null && param.content != "")
            {
                try
                {
                    WordDocument document = WordDocument.LoadString(param.content, GetFormatType(param.type.ToLower()));
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(document);
                    document.Dispose();
                    return json;
                }
                catch (Exception)
                {
                    return "";
                }
            }
            return "";
        }

        public class CustomRestrictParameter
        {
            public string passwordBase64 { get; set; }
            public string saltBase64 { get; set; }
            public int spinCount { get; set; }
        }

        public class UploadDocument
        {
            public string DocumentName { get; set; }
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("documentserver/[controller]/RestrictEditing")]
        public string[] RestrictEditing([FromBody] CustomRestrictParameter param)
        {
            if (param.passwordBase64 == "" && param.passwordBase64 == null)
                return null;
            return WordDocument.ComputeHash(param.passwordBase64, param.saltBase64, param.spinCount);
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("documentserver/[controller]/LoadDefault")]
        public string LoadDefault()
        {
            Stream stream = System.IO.File.OpenRead("App_Data/GettingStarted.docx");
            stream.Position = 0;

            WordDocument document = WordDocument.Load(stream, Syncfusion.EJ2.DocumentEditor.FormatType.Docx);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(document);
            document.Dispose();
            return json;
        }

        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("documentserver/[controller]/LoadDocument")]
        public string LoadDocument([FromForm] UploadDocument uploadDocument)
        {
            string documentPath = Path.Combine(path, uploadDocument.DocumentName);
            Stream stream = null;
            if (System.IO.File.Exists(documentPath))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(documentPath);
                stream = new MemoryStream(bytes);
            }
            else
            {
                bool result = Uri.TryCreate(uploadDocument.DocumentName, UriKind.Absolute, out Uri uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                if (result)
                {
                    stream = GetDocumentFromURL(uploadDocument.DocumentName).Result;
                    if (stream != null)
                        stream.Position = 0;
                }
            }
            WordDocument document = WordDocument.Load(stream, FormatType.Docx);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(document);
            document.Dispose();
            return json;
        }

        private async Task<MemoryStream> GetDocumentFromURL(string url)
        {
            var client = new HttpClient(); ;
            var response = await client.GetAsync(url);
            var rawStream = await response.Content.ReadAsStreamAsync();
            if (response.IsSuccessStatusCode)
            {
                MemoryStream docStream = new MemoryStream();
                rawStream.CopyTo(docStream);
                return docStream;
            }
            else { return null; }
        }

        internal static Syncfusion.EJ2.DocumentEditor.FormatType GetFormatType(string format)
        {
            if (string.IsNullOrEmpty(format))
                throw new NotSupportedException("EJ2 DocumentEditor does not support this file format.");
            switch (format.ToLower())
            {
                case ".dotx":
                case ".docx":
                case ".docm":
                case ".dotm":
                    return FormatType.Docx;

                case ".dot":
                case ".doc":
                    return FormatType.Doc;

                case ".rtf":
                    return FormatType.Rtf;

                case ".txt":
                    return FormatType.Txt;

                case ".xml":
                    return FormatType.WordML;

                case ".html":
                    return FormatType.Html;

                default:
                    throw new NotSupportedException("EJ2 DocumentEditor does not support this file format.");
            }
        }

        internal static WFormatType GetWFormatType(string format)
        {
            if (string.IsNullOrEmpty(format))
                throw new NotSupportedException("EJ2 DocumentEditor does not support this file format.");
            switch (format.ToLower())
            {
                case ".dotx":
                    return WFormatType.Dotx;

                case ".docx":
                    return WFormatType.Docx;

                case ".docm":
                    return WFormatType.Docm;

                case ".dotm":
                    return WFormatType.Dotm;

                case ".dot":
                    return WFormatType.Dot;

                case ".doc":
                    return WFormatType.Doc;

                case ".rtf":
                    return WFormatType.Rtf;

                case ".txt":
                    return WFormatType.Txt;

                case ".xml":
                    return WFormatType.WordML;

                case ".odt":
                    return WFormatType.Odt;

                default:
                    throw new NotSupportedException("EJ2 DocumentEditor does not support this file format.");
            }
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("documentserver/[controller]/Export")]
        public FileStreamResult Export(IFormCollection data)
        {
            if (data.Files.Count == 0)
                return null;
            string fileName = this.GetValue(data, "filename");
            string name = fileName;
            int index = name.LastIndexOf('.');
            string format = index > -1 && index < name.Length - 1 ?
                name.Substring(index) : ".doc";
            if (string.IsNullOrEmpty(name))
            {
                name = "Document1";
            }
            Stream stream = new MemoryStream();
            string contentType = "";
            WDocument document = this.GetDocument(data);
            if (format == ".pdf")
            {
                contentType = "application/pdf";
            }
            else
            {
                WFormatType type = GetWFormatType(format);
                switch (type)
                {
                    case WFormatType.Rtf:
                        contentType = "application/rtf";
                        break;

                    case WFormatType.WordML:
                        contentType = "application/xml";
                        break;

                    case WFormatType.Dotx:
                        contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.template";
                        break;

                    case WFormatType.Doc:
                        contentType = "application/msword";
                        break;

                    case WFormatType.Dot:
                        contentType = "application/msword";
                        break;
                }
                document.Save(stream, type);
            }
            document.Close();
            stream.Position = 0;

            return new FileStreamResult(stream, contentType)
            {
                FileDownloadName = fileName
            };
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("documentserver/[controller]/ExportToPdf")]
        public string ExportToPDF(IFormCollection data)
        {
            if (data.Files.Count == 0)
                return null;

            string fileName = this.GetValue(data, "filename");

            MemoryStream stream = new MemoryStream();
            WDocument document = this.GetDocument(data);
            Syncfusion.DocIORenderer.DocIORenderer docIORenderer = new DocIORenderer();
            var pdfDocument = docIORenderer.ConvertToPDF(document);
            pdfDocument.Save(stream);

            var pdf = stream.ToArray();
            //var contentType = "application/pdf";

            string base64String = Convert.ToBase64String(pdf);
            string base64 = String.Format("data:application/pdf;base64," + base64String);
            docIORenderer.Dispose();
            document.Dispose();
            pdfDocument.Dispose();

            stream.Close();
            return base64String;
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("documentserver/[controller]/PrintDicom")]
        public async Task<IActionResult> PrintDicomAsync(IFormCollection data)
        {
            try
            {
                if (data.Files.Count == 0) return null;

                string patientName = this.GetValue(data, "patientName").Trim().ToUpper();
                string studyDate = DateTime.Parse(this.GetValue(data, "studyDate").Trim().Substring(0, 10)).ToString("yyyyMMdd");
                var patientBirthDate = DateTime.Parse(this.GetValue(data, "patientBirthdate").Trim().Substring(0, 10));
                string studyId = this.GetValue(data, "studyId").Trim();
                string patientAge = new Func<string>(() =>
                {
                    var age = (DateTime.Now - patientBirthDate).Days;
                    if (age < 365)
                    {
                        return $"{(age / 30).ToString("N0")}M";
                    }
                    else
                    {
                        return $"{(age / 365).ToString("N0")}Y";
                    }
                })();
                string patientId = this.GetValue(data, "patientId").Trim();
                string patientGender = this.GetValue(data, "patientGender").Trim();
                string referringDoctor = new Func<string>(() =>
                  {
                      if (this.GetValue(data, "referringDoctor") == null)
                      {
                          return "";
                      }
                      else
                      {
                          return this.GetValue(data, "referringDoctor").Trim();
                      }
                  })();

                string patientAgeClient = this.GetValue(data, "patientAge").Trim().ToUpper();
                string name = $"{patientName} {patientAgeClient}";

                MemoryStream stream = new MemoryStream();
                WDocument document = this.GetDocument(data);
                Syncfusion.DocIORenderer.DocIORenderer docIORenderer = new DocIORenderer();
                var pdfDocument = docIORenderer.ConvertToPDF(document);
                pdfDocument.Save(stream);

                var pdf = stream.ToArray();
                //System.IO.File.WriteAllBytes(@$"D:\TEST\{DateTime.Now.ToFileTime()}.pdf", pdf);

                var dataset = new DicomDataset()
                {
                    {DicomTag.InstitutionName, "CIM ESPOIRE - Dr.LAGHOUATI" },
                    {DicomTag.InstitutionAddress, "Cité Ben-Sahnoun El-M'kam LAGHOUAT, ALGERIA"},
                    {DicomTag.PerformingPhysicianName, "Dr.LAGHOUATI. M" },
                    {DicomTag.StudyID, studyId },
                    {DicomTag.StudyDate, studyDate },
                    {DicomTag.PatientAge, patientAge.PadLeft(4, '0')},
                    {DicomTag.PatientBirthDate, patientBirthDate.ToString("yyyyMMdd")},
                    {DicomTag.PatientID, patientId},
                    {DicomTag.PatientSex, patientGender.Substring(0,1).ToUpper()},
                    {DicomTag.ReferringPhysicianName, ""},
                    {DicomTag.InstanceCreationTime,  DateTime.Now.ToString("HHmmss")},
                    {DicomTag.InstanceCreationDate,  DateTime.Now.ToString("yyyyMMdd")},
                    {DicomTag.ConversionType,"WSD" },
                    {DicomTag.Modality, "50LAB_RIS" },
                    {DicomTag.InstanceNumber, studyId},
                    {DicomTag.SeriesNumber, studyId },
                    {DicomTag.SpecificCharacterSet, "ISO_IR_100" },
                    {DicomTag.TransferSyntaxUID, "1.2.840.10008.1.2.1" },
                    {DicomTag.ImplementationVersionName, "50LAB_RIS" },
                    {DicomTag.SeriesInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID().UID  },
                    {DicomTag.StudyInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID().UID  },
                    {DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID().UID },
                    {DicomTag.SOPClassUID, "1.2.840.10008.5.1.4.1.1.104.1" },
                    {DicomTag.MediaStorageSOPClassUID,"1.2.840.10008.5.1.4.1.1.104.1" },
                    {DicomTag.MediaStorageSOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID().UID },
                    {DicomTag.PatientName, name },
                    {DicomTag.MIMETypeOfEncapsulatedDocument, "application/pdf" },
                    {DicomTag.EncapsulatedDocument,  pdf}
                };
                var dicom = new DicomFile(dataset);
                //dicom.Save(@$"D:\TEST\{DateTime.Now.ToFileTime()}.dcm");

                var config = ConfigModel.GetConfig();

                var client = new DicomClient(config.PrintServerHost, config.PrintServerPort, false, config.PrintServerAec, config.PrintServerAet);
                client.NegotiateAsyncOps();
                await client.AddRequestAsync(new DicomCStoreRequest(dicom));
                await client.SendAsync();

                stream.Dispose();
                pdfDocument.Close(true);
                pdfDocument.Dispose();
                document.Close();
                document.Dispose();

                return Ok(new { result = "Success", message = "" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { result = "Error", message = ex.Message });
            }
        }

        private string GetValue(IFormCollection data, string key)
        {
            if (data.ContainsKey(key))
            {
                string[] values = data[key];
                if (values.Length > 0)
                {
                    return values[0];
                }
            }
            return "";
        }

        private WDocument GetDocument(IFormCollection data)
        {
            MemoryStream stream = new MemoryStream();
            IFormFile file = data.Files[0];

            file.CopyTo(stream);
            stream.Position = 0;

            WDocument document = new WDocument(stream, WFormatType.Docx);

            //System.IO.File.WriteAllBytes(@$"D:\TEST\{DateTime.Now.ToFileTime()}.docx", stream.ToArray());
            stream.Dispose();

            return document;
        }
    }
}