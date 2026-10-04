using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using XWidgetReborn.Shared;

namespace XWidgetReborn
{
    internal static class NativeServiceManager
    {
        private const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        private const uint SERVICE_ALL_ACCESS = 0xF01FF;
        private const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
        private const uint SERVICE_AUTO_START = 0x00000002;
        private const uint SERVICE_ERROR_NORMAL = 0x00000001;
        private const uint SERVICE_CONTROL_STOP = 0x00000001;
        private const uint SERVICE_QUERY_STATUS = 0x00000004;
        private const uint DELETE = 0x00010000;

        private const string ServiceName = AppConstants.ServiceName;
        private const string DisplayName = AppConstants.ServiceDisplayName;
        private static readonly string ServiceDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "EmilyDesk", "WeatherService");
        private static readonly string ServiceDataDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
            "EmilyDesk", "WeatherService");

        public static string SecureServiceDirectory
        {
            get { return ServiceDirectory; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_STATUS
        {
            public uint dwServiceType;
            public uint dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenSCManager(
            string machineName,
            string databaseName,
            uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateService(
            IntPtr serviceControlManager,
            string serviceName,
            string displayName,
            uint desiredAccess,
            uint serviceType,
            uint startType,
            uint errorControl,
            string binaryPathName,
            string loadOrderGroup,
            IntPtr tagId,
            string dependencies,
            string serviceStartName,
            string password);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenService(
            IntPtr serviceControlManager,
            string serviceName,
            uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool StartService(
            IntPtr service,
            int numberOfServiceArgs,
            string[] serviceArgVectors);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool ControlService(
            IntPtr service,
            uint control,
            ref SERVICE_STATUS serviceStatus);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DeleteService(IntPtr service);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CloseServiceHandle(IntPtr handle);

        public static void Install(string serviceExecutable)
        {
            if (string.IsNullOrWhiteSpace(serviceExecutable) || !File.Exists(serviceExecutable))
                throw new FileNotFoundException("Weather service executable was not found.", serviceExecutable);

            Remove();

            IntPtr scm = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
            if (scm == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to open Service Control Manager.");

            try
            {
                string command = "\"" + Path.GetFullPath(serviceExecutable) + "\"";

                IntPtr service = CreateService(
                    scm,
                    ServiceName,
                    DisplayName,
                    SERVICE_ALL_ACCESS,
                    SERVICE_WIN32_OWN_PROCESS,
                    SERVICE_AUTO_START,
                    SERVICE_ERROR_NORMAL,
                    command,
                    null,
                    IntPtr.Zero,
                    null,
                    "LocalSystem",
                    null);

                if (service == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create the service.");

                CloseServiceHandle(service);
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }

        public static void DeployInstallAndStart(string payloadDirectory,
            string legacyUserDataDirectory)
        {
            if (string.IsNullOrWhiteSpace(payloadDirectory) ||
                !Directory.Exists(payloadDirectory))
                throw new DirectoryNotFoundException(
                    "Weather service payload was not found: " +
                    payloadDirectory);
            if (!SamePath(payloadDirectory, ServiceDirectory))
                throw new InvalidOperationException(
                    "The weather service may only be deployed from its " +
                    "protected Program Files directory.");

            string[] requiredFiles =
            {
                "EmilyDesk.Service.exe",
                "XWidgetReborn.WeatherCore.dll",
                "XWidgetReborn.Shared.dll"
            };
            foreach (string fileName in requiredFiles)
            {
                string source = Path.Combine(payloadDirectory, fileName);
                if (!File.Exists(source))
                    throw new FileNotFoundException(
                        "Weather service dependency is missing.", source);
            }

            Remove();
            Directory.CreateDirectory(ServiceDirectory);
            ApplySecureDirectoryPermissions(ServiceDirectory);
            foreach (string fileName in requiredFiles)
            {
                string source = Path.Combine(payloadDirectory, fileName);
                string target = Path.Combine(ServiceDirectory, fileName);
                if (!SamePath(source, target))
                    File.Copy(source, target, true);
            }

            string sourceProfiles = Path.Combine(payloadDirectory,
                "ProviderProfiles");
            string targetProfiles = Path.Combine(ServiceDirectory,
                "ProviderProfiles");
            if (!Directory.Exists(sourceProfiles))
                throw new DirectoryNotFoundException(
                    "Weather provider profiles are missing.");
            Directory.CreateDirectory(targetProfiles);
            foreach (string source in Directory.GetFiles(
                sourceProfiles, "*.json"))
            {
                string target = Path.Combine(targetProfiles,
                    Path.GetFileName(source));
                if (!SamePath(source, target))
                    File.Copy(source, target, true);
            }
            ApplySecureDirectoryPermissions(ServiceDirectory);
            Directory.CreateDirectory(ServiceDataDirectory);
            MigrateLegacyWeatherData(legacyUserDataDirectory);
            ApplySecureDirectoryPermissions(ServiceDataDirectory);

            ConfigureUrlReservation();
            RemoveLegacyHostsEntry();
            Install(Path.Combine(ServiceDirectory,
                "EmilyDesk.Service.exe"));
            Start();
        }

        private static void MigrateLegacyWeatherData(string legacyDirectory)
        {
            if (string.IsNullOrWhiteSpace(legacyDirectory) ||
                !Directory.Exists(legacyDirectory)) return;
            foreach (string fileName in new[] {
                "locations.json", "provider-settings.json" })
            {
                string source = Path.Combine(legacyDirectory, fileName);
                string target = Path.Combine(ServiceDataDirectory, fileName);
                if (File.Exists(source) && !File.Exists(target))
                    File.Copy(source, target, false);
            }
            string sourceCache = Path.Combine(legacyDirectory, "cache");
            string targetCache = Path.Combine(ServiceDataDirectory, "cache");
            if (!Directory.Exists(sourceCache)) return;
            Directory.CreateDirectory(targetCache);
            foreach (string source in Directory.GetFiles(
                sourceCache, "*.json"))
            {
                string target = Path.Combine(targetCache,
                    Path.GetFileName(source));
                if (!File.Exists(target)) File.Copy(source, target, false);
            }
        }

        private static bool SamePath(string left, string right)
        {
            return string.Equals(Path.GetFullPath(left).TrimEnd(
                    Path.DirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(
                    Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        public static void RemoveDeployment()
        {
            Remove();
            DeleteUrlReservation(AppConstants.ListenerPrefix);
            DeleteUrlReservation("http://api.accuweather.com:80/");
            RemoveLegacyHostsEntry();
            if (Directory.Exists(ServiceDirectory))
                Directory.Delete(ServiceDirectory, true);
        }

        private static void ConfigureUrlReservation()
        {
            DeleteUrlReservation(AppConstants.ListenerPrefix);
            RunNetsh("http add urlacl url=" +
                AppConstants.ListenerPrefix +
                " sddl=D:(A;;GX;;;SY)", true);
            DeleteUrlReservation("http://api.accuweather.com:80/");
        }

        private static void DeleteUrlReservation(string prefix)
        {
            RunNetsh("http delete urlacl url=" + prefix, false);
        }

        private static void RunNetsh(string arguments, bool required)
        {
            var start = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.System), "netsh.exe"),
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = Process.Start(start))
            {
                string error = process.StandardError.ReadToEnd();
                process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (required && process.ExitCode != 0)
                    throw new InvalidOperationException(
                        "Unable to reserve the local weather endpoint. " +
                        error);
            }
        }

        private static void ApplySecureDirectoryPermissions(string path)
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            InheritanceFlags inheritance = InheritanceFlags.ContainerInherit |
                InheritanceFlags.ObjectInherit;
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl, inheritance,
                PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(
                    WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl, inheritance,
                PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                FileSystemRights.ReadAndExecute |
                    FileSystemRights.Synchronize, inheritance,
                PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(path).SetAccessControl(security);
        }

        private static void RemoveLegacyHostsEntry()
        {
            string hosts = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.System),
                @"drivers\etc\hosts");
            if (!File.Exists(hosts)) return;
            string[] lines = File.ReadAllLines(hosts);
            bool containsLegacyEntry = false;
            foreach (string line in lines)
                if (line.IndexOf("api.accuweather.com",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    containsLegacyEntry = true;
                    break;
                }
            if (!containsLegacyEntry) return;
            string temporary = hosts + ".emilydesk-" +
                Guid.NewGuid().ToString("N") + ".tmp";
            using (var writer = new StreamWriter(temporary, false))
                foreach (string line in lines)
                    if (line.IndexOf("api.accuweather.com",
                        StringComparison.OrdinalIgnoreCase) < 0)
                        writer.WriteLine(line);
            File.Copy(temporary, hosts, true);
            File.Delete(temporary);
        }

        public static void Start()
        {
            using (var controller = new ServiceController(ServiceName))
            {
                controller.Refresh();
                if (controller.Status == ServiceControllerStatus.Running)
                    return;

                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }
        }

        public static void Stop()
        {
            try
            {
                using (var controller = new ServiceController(ServiceName))
                {
                    controller.Refresh();
                    if (controller.Status == ServiceControllerStatus.Stopped)
                        return;

                    controller.Stop();
                    controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }
            }
            catch (InvalidOperationException)
            {
                // Service is not installed.
            }
        }

        public static void Remove()
        {
            Stop();

            IntPtr scm = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
            if (scm == IntPtr.Zero)
                return;

            try
            {
                IntPtr service = OpenService(scm, ServiceName, DELETE | SERVICE_QUERY_STATUS);
                if (service == IntPtr.Zero)
                    return;

                try
                {
                    if (!DeleteService(service))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != 1072 && error != 1060)
                            throw new Win32Exception(error, "Unable to delete the service.");
                    }
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scm);
            }

            Thread.Sleep(1000);
        }
    }
}
