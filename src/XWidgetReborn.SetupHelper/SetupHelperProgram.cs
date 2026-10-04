using System;
using System.IO;
using XWidgetReborn.Shared;

namespace XWidgetReborn
{
    internal static class SetupHelperProgram
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Console.Error.WriteLine(
                    "Usage: EmilyDesk.SetupHelper.exe " +
                        "deploy-service-and-verify <payload-directory> | " +
                        "start-and-verify | verify-service | stop-service | " +
                        "remove-service | apply-xwidget-icon <xwidget-exe> " +
                        "<icon-file> | restore-xwidget-theme <xwidget-exe>");
                    return 2;
                }

                string command = args[0].Trim().ToLowerInvariant();

                switch (command)
                {
                    case "deploy-service-and-verify":
                        if (args.Length < 2)
                            throw new ArgumentException(
                                "The service payload directory is required.");
                        NativeServiceManager.DeployInstallAndStart(args[1],
                            args.Length >= 3 ? args[2] : null);
                        ServiceVerifier.VerifyOrThrow();
                        break;

                    case "start-service":
                        NativeServiceManager.Start();
                        break;

                    case "start-and-verify":
                        NativeServiceManager.Start();
                        ServiceVerifier.VerifyOrThrow();
                        break;

                    case "verify-service":
                        ServiceVerifier.VerifyOrThrow();
                        break;

                    case "stop-service":
                        NativeServiceManager.Stop();
                        break;

                    case "remove-service":
                        NativeServiceManager.Remove();
                        break;

                    case "remove-service-deployment":
                        NativeServiceManager.RemoveDeployment();
                        break;

                    case "apply-xwidget-theme":
                        if (args.Length < 3)
                            throw new ArgumentException(
                                "XWidget executable and theme directory paths are required.");
                        IconResourcePatcher.Apply(args[1], args[2]);
                        break;

                    case "restore-xwidget-theme":
                        if (args.Length < 2)
                            throw new ArgumentException(
                                "The XWidget executable path is required.");
                        IconResourcePatcher.Restore(args[1]);
                        break;

                    default:
                        throw new ArgumentException("Unknown command: " + args[0]);
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }
    }
}
