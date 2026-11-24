#if STORE_IL2CPP
using System;

namespace HSRTimer
{
    internal enum StoreHudFontStyle { Normal, Bold }

    /// <summary>Persistent intent only. Asset names are Unity identifiers, never filesystem paths.</summary>
    internal sealed class StoreHudFontPreference
    {
        internal static readonly StoreHudFontPreference Imgui = new(null, null, StoreHudFontStyle.Bold);
        private const string InvalidNameCharacters = "<>:\"|?*";

        internal bool IsExternal => BundleFileName != null;
        internal string BundleFileName { get; }
        internal string AssetName { get; }
        internal StoreHudFontStyle Style { get; }

        private StoreHudFontPreference(string bundleFileName, string assetName, StoreHudFontStyle style)
        {
            BundleFileName = bundleFileName;
            AssetName = assetName;
            Style = style;
        }

        internal static bool TryExternal(string bundle, string asset, StoreHudFontStyle style, out StoreHudFontPreference preference)
        {
            preference = Imgui;
            if ((style != StoreHudFontStyle.Normal && style != StoreHudFontStyle.Bold)
                || !IsNameSegment(bundle) || bundle.IndexOf('/') >= 0
                || !bundle.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase)
                || bundle.Length <= ".bundle".Length || IsWindowsDeviceName(bundle)
                || string.IsNullOrEmpty(asset))
                return false;

            foreach (string segment in asset.Split('/'))
                if (!IsNameSegment(segment))
                    return false;

            preference = new StoreHudFontPreference(bundle, asset, style);
            return true;
        }

        internal static StoreHudFontPreference Read(string mode, string bundle, string asset, string style)
        {
            if (!string.Equals(mode, "external", StringComparison.OrdinalIgnoreCase))
                return Imgui;
            // Missing or unknown style uses the external font's own weight.
            var weight = string.Equals(style, "bold", StringComparison.OrdinalIgnoreCase)
                ? StoreHudFontStyle.Bold : StoreHudFontStyle.Normal;
            TryExternal(bundle, asset, weight, out var preference);
            return preference;
        }

        private static bool IsNameSegment(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim() != name || name.EndsWith(".", StringComparison.Ordinal))
                return false;
            foreach (char character in name)
                if (char.IsControl(character) || character == '\\' || InvalidNameCharacters.IndexOf(character) >= 0)
                    return false;
            return true;
        }

        private static bool IsWindowsDeviceName(string fileName)
        {
            string stem = fileName.Substring(0, fileName.IndexOf('.')).TrimEnd().ToUpperInvariant();
            return stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL"
                || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal)
                    || stem.StartsWith("LPT", StringComparison.Ordinal)) && "123456789¹²³".IndexOf(stem[3]) >= 0);
        }
    }
}
#endif
