using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace UATL.Mail.Helpers;

public class ImageHelper
{
    public static async Task<Stream> EncodeWebp(IFormFile file, CancellationToken ct)
    {
        var ms = new MemoryStream();

        using (var stream = file.OpenReadStream())
        using (var image = Image.Load(stream))
        {
            if (image.Size().Width > 512 || image.Size().Height > 0)
                image.Mutate(x => x.Resize(ResizeKeepAspect(image.Size())));
            await image.SaveAsWebpAsync(ms, new WebpEncoder {Method = WebpEncodingMethod.Level6}, ct)
                .ConfigureAwait(false);

            return ms;
        }
    }

    private static Size ResizeKeepAspect(Size src, int maxWidth = 512, int maxHeight = 512, bool enlarge = false)
    {
        maxWidth = enlarge ? maxWidth : Math.Min(maxWidth, src.Width);
        maxHeight = enlarge ? maxHeight : Math.Min(maxHeight, src.Height);

        var rnd = Math.Min(maxWidth / (double) src.Width, maxHeight / (double) src.Height);

        return new Size((int) Math.Round(src.Width * rnd), (int) Math.Round(src.Height * rnd));
    }
}