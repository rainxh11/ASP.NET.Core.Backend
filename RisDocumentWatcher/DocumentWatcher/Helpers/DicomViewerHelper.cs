using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentWatcher.Helpers
{
    public class DicomViewerHelper
    {
        public static FileInfo GetDicomViewer(string name = "RadiAntViewer.exe")
        {
            var file = new DirectoryInfo(Path.GetPathRoot(Environment.SystemDirectory))
                .GetFiles(name, SearchOption.AllDirectories)
                .First();

            return file;
        }
    }
}
