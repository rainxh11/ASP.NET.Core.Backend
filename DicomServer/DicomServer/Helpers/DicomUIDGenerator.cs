using System;
namespace DicomServer.Helpers
{
    public class DicomUIDGenerator
    {
        public static string Generate(string studyId, DateTimeOffset studyDate, int increment)
        {
            var uid = $"2.25.50.0.{studyId}.{increment}.{studyDate.ToUnixTimeMilliseconds()}";
            return uid;
        }
    }
}
