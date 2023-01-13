using DicomServer.Helper;
using System.Linq;

namespace DicomServer.Helpers
{
    public class ModalityHelper
    {

        public static bool AllowModality(string modality)
        {
            var config = ConfigHelper.GetConfig();

            return config.Modalities.Any(x => x.Name.Contains(modality, System.StringComparison.OrdinalIgnoreCase));
        }
        public static string GetModalityTag(string modality)
        {
            try
            {
                return ConfigHelper.GetConfig().Modalities.Find(x => x.Name.Contains(modality)).Tag;
            }
            catch
            {
                return modality;
            }
        }
    }
}
