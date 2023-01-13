using DicomServer.Helper;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Invio.Hashing;

namespace DicomServer.Helpers
{
    public class PACSStorageStatus : IEquatable<PACSStorageStatus>
    {
        public long PACSUsedSpace { get; set; }
        public long DriveSpace { get; set; }
        public long DriveFreeSpace { get; set; }
        public long DriveUsedSpace { get => DriveSpace - DriveFreeSpace; }
        public decimal PACSPercentage { get; set; }
        public decimal DriveNonPACSPercentage { get; set; }

        public bool Equals(PACSStorageStatus other)
        {
            return PACSUsedSpace == other.PACSUsedSpace && DriveSpace == other.DriveSpace && DriveFreeSpace == other.DriveFreeSpace && DriveUsedSpace == other.DriveUsedSpace;
        }
        public override int GetHashCode()
        {
            unchecked
            {
                return Invio.Hashing.HashCode.From(PACSUsedSpace, DriveFreeSpace, DriveUsedSpace, DriveSpace);
            }
        }
    }
    public class PACSStatusHelper
    {
        public static async Task<PACSStorageStatus> GetStorageStatus()
        {
            try
            {
                var config = ConfigHelper.GetConfig();

                var pacsFolder = new DirectoryInfo(config.PACSFolder);
                var pacsDrive = new DriveInfo(pacsFolder.Root.FullName);

                
                var driverUsedSpace = pacsDrive.TotalSize - pacsDrive.TotalFreeSpace;

                //Disabled because calcualting size of each file in 1 Million files on slow external USB 2.0 Hard Drive is just fucking absurd & never finish
                var pacsSize = await Task.Run(() => pacsFolder.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length));
                //var pacsSize = driverUsedSpace;

                return new PACSStorageStatus()
                {
                    PACSUsedSpace = pacsSize,
                    DriveSpace = pacsDrive.TotalSize,
                    DriveFreeSpace = pacsDrive.TotalFreeSpace,
                    PACSPercentage = Math.Round(Convert.ToDecimal(pacsSize / pacsDrive.TotalSize), 2),
                    DriveNonPACSPercentage = Math.Round(Convert.ToDecimal((driverUsedSpace - pacsSize) / pacsDrive.TotalSize), 2)
                };
            }
            catch
            {
                return null;
            }
            
        }
    }
}
