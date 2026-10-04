namespace XWidgetReborn.Runtime.Core
{
    internal sealed class WidgetPluginManifest
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string version { get; set; }
        public string author { get; set; }
        public string assembly { get; set; }
        public string type { get; set; }
        public string minimumSdkVersion { get; set; }
        public string icon { get; set; }
        public string preview { get; set; }
        public bool enabledByDefault { get; set; }
        public string category { get; set; }
        public bool official { get; set; }
        public bool bundled { get; set; }
        public string minimumEngineVersion { get; set; }
        public string[] capabilities { get; set; }
    }
}
