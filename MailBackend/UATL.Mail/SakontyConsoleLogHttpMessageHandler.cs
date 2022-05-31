using System.Diagnostics;
using System.Drawing;
using Colorful;
using Console = Colorful.Console;

namespace UATL.MailSystem;

public class ConsoleFormatter
{
    public static Formatter FormatStatusCode(int code)
    {
        switch (code)
        {
            case var number when number >= 100 && number < 200:
                return new Formatter($"{code}", Color.Cyan);

            case var number when number >= 200 && number < 300:
                return new Formatter($"{code}", Color.LimeGreen);

            case var number when number >= 300 && number < 400:
                return new Formatter($"{code}", Color.Purple);

            case var number when number >= 400 && number < 500:
                return new Formatter($"{code}", Color.Orange);

            default:
            case var number when number >= 500 && number < 600:
                return new Formatter($"{code}", Color.Red);
        }
    }
}

public class SakonyConsoleMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        Stopwatch sw = new();

        var routePath = context.Request.Path.ToString();
        var method = context.Request.Method;

        sw.Start();
        await next(context).ConfigureAwait(false);
        sw.Stop();
        var response = context.Response;

        var contentLength = "-";
        if (response.Headers.Any(x => x.Key == "Content-Length"))
            contentLength = response.Headers.First(x => x.Key == "Content-Length").Value.First() + " bytes";
        var responseCode = response.StatusCode;

        var log = @$"[{method}] {routePath} - {{0}} {sw.ElapsedMilliseconds:N0} ms - {contentLength}";
        Console.WriteLineFormatted(log, Color.White, ConsoleFormatter.FormatStatusCode(responseCode));
    }
}

public class SakontyConsoleLogHttpMessageHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Stopwatch sw = new();

        var routePath = request.RequestUri?.PathAndQuery;
        var method = request.Method;

        sw.Start();
        var response = await base.SendAsync(request, cancellationToken);
        sw.Stop();

        var contentLength = "-";
        if (response.Headers.Any(x => x.Key == "Content-Length"))
            contentLength = response.Headers.First(x => x.Key == "Content-Length").Value.First();
        var responseCode = response.StatusCode;

        var log = @$"{method.Method}\t {routePath} \t {{0}} {sw.ElapsedMilliseconds:N0} ms - {contentLength} bytes";
        Console.WriteLineFormatted(log, Color.White, ConsoleFormatter.FormatStatusCode((int) responseCode));

        return response;
    }
}