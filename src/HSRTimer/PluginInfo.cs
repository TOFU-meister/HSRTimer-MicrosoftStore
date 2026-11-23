namespace HSRTimer
{
    /// <summary>
    /// Static plugin identity constants used by the BepInEx plugin attribute
    /// and surfaced to the UI (e.g. the {pluginversion} template variable and the
    /// settings panel's About page).
    /// </summary>
    internal static class PluginInfo
    {
        public const string PLUGIN_GUID = "HSRTimer";
        public const string PLUGIN_NAME = "HSRTimer";
        public const string PLUGIN_VERSION = "0.0.0";
#if STORE_IL2CPP
        public const string StoreVersion = "0.1.2-pre";
        internal const string RuntimeVersion = StoreVersion;
        internal const string RuntimeName = "HSRTimer Microsoft Store";
        internal const string PLUGIN_REPOSITORY_URL = "https://github.com/TOFU-meister/HSRTimer-MicrosoftStore";
#else
        internal const string RuntimeVersion = PLUGIN_VERSION;
        internal const string RuntimeName = PLUGIN_NAME;
        internal const string PLUGIN_REPOSITORY_URL = UpstreamRepositoryUrl;
#endif
        internal const string UpstreamRepositoryUrl = "https://github.com/TwilightCup/HSRTimer";

        // R12.3: first two non-empty lines of the repository LICENSE file.
        // License notices are legal text and intentionally not localized; keep
        // these in sync with LICENSE when the copyright line changes.
        public const string LICENSE_LINE1 = "MIT License";
        public const string LICENSE_LINE2 = "Copyright (c) 2026 TwilightCup";
    }
}
