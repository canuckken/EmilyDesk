using System;

namespace XWidgetReborn.Runtime.Core
{
    internal interface IRuntimePackageManager
    {
        void RefreshInstalledPackages();
    }

    // Package archive validation and file transactions remain in the Dashboard.
    // The Runtime owns only the activation boundary: refreshing its installed
    // package catalog after the Dashboard completes a transaction.
    internal sealed class RuntimePackageManager : IRuntimePackageManager
    {
        private readonly IWidgetRegistry _registry;
        private readonly IRuntimeLoggingService _logging;

        public RuntimePackageManager(
            IWidgetRegistry registry,
            IRuntimeLoggingService logging)
        {
            if (registry == null) throw new ArgumentNullException("registry");
            if (logging == null) throw new ArgumentNullException("logging");
            _registry = registry;
            _logging = logging;
        }

        public void RefreshInstalledPackages()
        {
            _logging.Information("Refreshing installed widget packages.");
            _registry.Refresh();
        }
    }
}
