using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DicomServer
{
    public static class Run
    {
        private static bool IsDotNetFx =>
            RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase);

        private static readonly TaskFactory factory =
            new TaskFactory(
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);

        public static TResult Sync<TResult>(Func<Task<TResult>> func)
        {
            if (IsDotNetFx)
            {
                return factory.StartNew(func).Unwrap().GetAwaiter().GetResult();
            }
            else
            {
                return func().GetAwaiter().GetResult();
            }
        }

        public static void Sync(Func<Task> func)
        {
            if (IsDotNetFx)
            {
                factory.StartNew(func).Unwrap().GetAwaiter().GetResult();
            }
            else
            {
                func().GetAwaiter().GetResult();
            }
        }
    }
}
