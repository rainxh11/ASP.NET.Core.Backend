using Microsoft.AspNetCore.Http.Features;

namespace UATL.MailSystem;

public class MailAttachementVerificationMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Features.Get<IHttpMaxRequestBodySizeFeature>().MaxRequestBodySize = null;

        try
        {
            if (context.Request.Form.Files.Any(file => file.Length > 200_000_000))
            {
                await context.Response.WriteAsJsonAsync(new
                    {Message = "File Attachments cannot exceed 200 MB per file."});
                context.Response.StatusCode = 403;
                await context.Response.CompleteAsync();
            }
        }
        catch
        {
        }

        await next(context);
    }
}