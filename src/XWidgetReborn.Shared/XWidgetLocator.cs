using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace XWidgetReborn.Shared
{
    public static class XWidgetLocator
    {
        public static string FindExecutable()
        {
            foreach (string candidate in EnumerateCandidateExecutables())
            {
                if (!string.IsNullOrWhiteSpace(candidate) &&
                    File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        public static bool IsRunning()
        {
            try
            {
                Process[] processes =
                    Process.GetProcessesByName("xwidget");

                return processes != null && processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryLaunch(out string error)
        {
            error = null;

            if (IsRunning())
                return true;

            string executable = FindExecutable();
            if (string.IsNullOrWhiteSpace(executable))
            {
                error = "The original XWidget executable could not be found.";
                return false;
            }

            try
            {
                Process.Start(executable);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static IEnumerable<string> EnumerateCandidateExecutables()
        {
            string registryCandidate = ReadRegistryCandidate();
            if (!string.IsNullOrWhiteSpace(registryCandidate))
            {
                if (registryCandidate.EndsWith(
                    ".exe",
                    StringComparison.OrdinalIgnoreCase))
                {
                    yield return registryCandidate;
                }
                else
                {
                    yield return Path.Combine(
                        registryCandidate,
                        "xwidget.exe");
                }
            }

            yield return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86),
                "XWidget",
                "xwidget.exe");

            yield return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles),
                "XWidget",
                "xwidget.exe");

            yield return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "XWidget",
                "xwidget.exe");
        }

        private static string ReadRegistryCandidate()
        {
            string[] subKeys =
            {
                @"SOFTWARE\XWidget",
                @"SOFTWARE\WOW6432Node\XWidget"
            };

            RegistryView[] views =
            {
                RegistryView.Registry32,
                RegistryView.Registry64
            };

            foreach (RegistryView view in views)
            {
                try
                {
                    using (RegistryKey baseKey =
                        RegistryKey.OpenBaseKey(
                            RegistryHive.LocalMachine,
                            view))
                    {
                        foreach (string subKey in subKeys)
                        {
                            using (RegistryKey key =
                                baseKey.OpenSubKey(subKey))
                            {
                                if (key == null)
                                    continue;

                                object value =
                                    key.GetValue("InstallPath") ??
                                    key.GetValue("Path") ??
                                    key.GetValue(null);

                                string result = value as string;
                                if (!string.IsNullOrWhiteSpace(result))
                                    return result.Trim('"');
                            }
                        }
                    }
                }
                catch
                {
                    // Registry lookup is optional.
                }
            }

            return null;
        }
    }
}
