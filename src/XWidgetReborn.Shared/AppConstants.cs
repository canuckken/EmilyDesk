namespace XWidgetReborn.Shared
{
    public static class AppConstants
    {
        public const string ProductName = "EmilyDesk";
        public const string Version = "2.10.31";
        public const string BuildId = "1377";
        public const string BuildDisplay = "Engine 2.10.31 Build 1377";
        public const string ServiceName = "XWidgetWeatherBridge";
        public const string ServiceDisplayName = "EmilyDesk Weather Core";
        public const string BridgeBaseUrl = "http://127.0.0.1:45873";
        public const string ListenerPrefix = BridgeBaseUrl + "/";
        public const string DashboardMutex = @"Local\XWidgetRebornDashboard";
        public const string DashboardShowEvent = @"Local\XWidgetRebornShowDashboard";
        public const string DashboardExitEvent = @"Local\XWidgetRebornExitDashboard";
        public const string RuntimeMutex = @"Local\XWidgetRebornRuntime";
        public const string RuntimeExecutableName = "EmilyDesk.Engine.exe";
        public const string RuntimeOpenClockEvent = @"Local\XWidgetRebornEngineOpenClock";
        public const string RuntimeCloseClockEvent = @"Local\XWidgetRebornEngineCloseClock";
        public const string RuntimeCommandEvent = @"Local\XWidgetRebornEngineCommand";
        public const string RuntimeExitEvent = @"Local\XWidgetRebornEngineExit";
        public const string RuntimeStatusFileName = "engine.status";
        public const string RuntimeCommandDirectoryName = "Commands";
    }
}
