#if STORE_IL2CPP
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;

namespace HSRTimer
{
    internal static class StoreGameVersion
    {
        private static string _display;

        internal static string Display => _display ??= ReadDisplay();

        private static string ReadDisplay()
        {
            try
            {
                var asset = Resources.Load<TextAsset>("Curve/build_version_number");
                if (asset != null && TryFormat(asset.text, out string display))
                    return display;
                Plugin.Logger.LogWarning("HSRTimer: game version resource was missing or invalid.");
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: could not read the game version: {ex.Message}");
            }
            return "?";
        }

        internal static bool TryFormat(string text, out string display)
        {
            display = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var document = new XmlDocument();
            try
            {
                using var reader = XmlReader.Create(new StringReader(text),
                    new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
                document.Load(reader);
            }
            catch (XmlException) { return false; }

            var version = document.DocumentElement;
            if (version?.LocalName != "VersionNumber") return false;

            var digits = new System.Text.StringBuilder();
            foreach (string field in new[] { "majorVersion", "minorVersion", "build", "revision" })
            {
                if (!int.TryParse(version[field]?.InnerText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
                    || number < 0)
                    return false;
                digits.Append(number.ToString(CultureInfo.InvariantCulture));
            }

            // Match the reference timer's v + seven-digit display. Shorter
            // versions can be displayed in full without slicing past the end.
            display = "v" + digits.ToString(0, System.Math.Min(7, digits.Length));
            return true;
        }
    }
}
#endif
