using System;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using XWidgetReborn.Shared;

namespace XWidgetReborn
{
    internal static class ServiceVerifier
    {
        private const string StatusUrl =
            AppConstants.BridgeBaseUrl + "/xwidgetbridge/status.json";

        private static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
            "EmilyDesk", "WeatherService");

        private static readonly string LogPath = Path.Combine(
            DataDirectory,
            "setup-diagnostics.log");

        public static void VerifyOrThrow()
        {
            Directory.CreateDirectory(DataDirectory);
            Log("VERIFY_BEGIN", "Checking service and local API.");

            Exception lastError = null;

            for (int attempt = 1; attempt <= 20; attempt++)
            {
                try
                {
                    using (var service =
                        new ServiceController(AppConstants.ServiceName))
                    {
                        service.Refresh();
                        Log(
                            "SERVICE_STATUS",
                            "Attempt " + attempt + ": " + service.Status);

                        if (service.Status !=
                            ServiceControllerStatus.Running)
                        {
                            Thread.Sleep(750);
                            continue;
                        }
                    }

                    using (var client = new TimeoutWebClient(3000))
                    {
                        client.Encoding = Encoding.UTF8;
                        string response =
                            client.DownloadString(StatusUrl);

                        if (response.IndexOf(
                            "\"status\":\"running\"",
                            StringComparison.OrdinalIgnoreCase) >= 0 ||
                            response.IndexOf(
                            "\"status\": \"running\"",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Log(
                                "VERIFY_SUCCESS",
                                "Local API responded successfully.");
                            return;
                        }

                        throw new InvalidOperationException(
                            "The status endpoint returned an unexpected payload.");
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Log(
                        "VERIFY_RETRY",
                        "Attempt " + attempt + ": " + ex.Message);
                    Thread.Sleep(750);
                }
            }

            string message =
                "The weather service was installed but its local API did not " +
                "become healthy within 15 seconds." +
                Environment.NewLine + Environment.NewLine +
                "Diagnostic log:" + Environment.NewLine +
                LogPath;

            if (lastError != null)
                message += Environment.NewLine + Environment.NewLine +
                    "Last error: " + lastError.Message;

            Log("VERIFY_FAILED", message);
            throw new InvalidOperationException(message, lastError);
        }

        private static void Log(string eventName, string message)
        {
            try
            {
                File.AppendAllText(
                    LogPath,
                    DateTime.UtcNow.ToString("o") +
                    " [" + eventName + "] " +
                    message + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
                // Diagnostics must never hide the original error.
            }
        }

        private sealed class TimeoutWebClient : WebClient
        {
            private readonly int _timeout;

            public TimeoutWebClient(int timeout)
            {
                _timeout = timeout;
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                if (request != null)
                    request.Timeout = _timeout;
                return request;
            }
        }
    }
}
