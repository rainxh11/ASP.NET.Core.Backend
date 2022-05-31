using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace UATL.MailSystem;

public static class Run
{
    private static readonly TaskFactory factory =
        new(
            CancellationToken.None,
            TaskCreationOptions.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);

    private static bool IsDotNetFx =>
        RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase);

    public static TResult Sync<TResult>(Func<Task<TResult>> func)
    {
        if (IsDotNetFx)
            return factory.StartNew(func).Unwrap().GetAwaiter().GetResult();
        return func().GetAwaiter().GetResult();
    }

    public static void Sync(Func<Task> func)
    {
        if (IsDotNetFx)
            factory.StartNew(func).Unwrap().GetAwaiter().GetResult();
        else
            func().GetAwaiter().GetResult();
    }
}