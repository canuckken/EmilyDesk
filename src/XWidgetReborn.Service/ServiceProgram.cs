using System;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using XWidgetWeatherBridgeV2;

namespace XWidgetReborn.ServiceHost
{
    internal static class ServiceProgram
    {
        private static void Main(string[] args)
        {
            bool consoleMode = args.Any(a => string.Equals(
                a, "--console", StringComparison.OrdinalIgnoreCase));

            if (consoleMode)
            {
                using (var host = new BridgeHost())
                {
                    host.Start();
                    Console.WriteLine("EmilyDesk Weather Core 2.4.0 is running.");
                    Console.WriteLine("Press Ctrl+C to stop.");

                    var quit = new ManualResetEvent(false);
                    Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e)
                    {
                        e.Cancel = true;
                        quit.Set();
                    };

                    quit.WaitOne();
                    host.Stop();
                }
                return;
            }

            if (Environment.UserInteractive)
            {
                Console.Error.WriteLine(
                    "This executable is the EmilyDesk Windows service. " +
                    "Use --console only for diagnostics.");
                return;
            }

            ServiceBase.Run(new BridgeService());
        }
    }
}
