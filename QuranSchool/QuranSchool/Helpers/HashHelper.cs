using System.Security.Cryptography;

namespace QuranSchool.Helpers;

public class HashHelper
{
    public static string CalculateFileFormMd5(IFormFile fileForm)
    {
        using (var stream = fileForm.OpenReadStream())
        using (var md5 = MD5.Create())
        {
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "");
        }
    }
}