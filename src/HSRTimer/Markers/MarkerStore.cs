using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HSRTimer
{
    /// <summary>
    /// File I/O for marker data (R10.1/R10.3.4, §4.1). One JSON file per
    /// (level key, category key). Failures are contained: a bad file never
    /// affects timing or the settings panel's other pages.
    ///
    /// Serialization uses <see cref="MarkerJson"/>, not <c>JsonUtility</c>: in
    /// this Unity version JsonUtility silently drops fields it cannot map
    /// (empty <c>List&lt;T&gt;</c> fields, and element types containing
    /// <c>long</c>/<c>uint</c>), which made saved marker files lose their
    /// <c>markers</c> array entirely.
    /// </summary>
    public static class MarkerStore
    {
        public const int FormatVersion = MarkerJson.FormatVersion;

        /// <summary>
        /// Normalized category key (R8.2.4): "Any" with no enabled tags,
        /// otherwise the sorted enabled tag ids joined with '+'.
        /// </summary>
        public static string CategoryKey()
        {
            var cfg = ConfigService.Instance;
            return SubsegmentFileStore.CategoryKey(cfg?.EnabledTags?.Tags);
        }

        /// <summary>Resolve the configured markers data root directory.</summary>
        public static string MarkersDir()
        {
            var cfg = ConfigService.Instance;
            string path = cfg != null && !string.IsNullOrEmpty(cfg.Settings.MarkersPath)
                ? cfg.Settings.MarkersPath
                : "markers";
#if STORE_IL2CPP
            return StoreDataPolicy.ResolveRecordPath(PersistenceService.PluginDir, path, "markers");
#else
            return SubsegmentFileStore.ResolvePath(path);
#endif
        }

        /// <summary>
        /// File path for a level's marker set. <paramref name="levelKey"/> is
        /// sanitized again defensively (idempotent) so a caller-provided raw
        /// level id also lands on the canonical path.
        /// </summary>
        public static string SetPath(string levelKey, string categoryKey)
        {
            string level = SubsegmentFileStore.SanitizeId(levelKey ?? "_");
            string category = SubsegmentFileStore.SanitizeId(categoryKey ?? "Any");
            return Path.Combine(MarkersDir(), level, category + ".json");
        }

        /// <summary>Build a fresh, empty marker set for a level.</summary>
        public static MarkerSet CreateEmpty(string levelKey, string levelSource, int levelNumber, string categoryKey)
        {
            var set = new MarkerSet
            {
                format_version = FormatVersion,
                level_id = levelKey ?? "",
                level_source = levelSource ?? "",
                level_number = levelNumber,
                category_key = categoryKey ?? "Any",
                hsrtimer_version = PluginInfo.RuntimeVersion,
                updated_at = UtcNow(),
            };
            return set;
        }

        /// <summary>
        /// Load a marker set. Returns false (and a null set) when the file is
        /// missing, malformed, or uses an unsupported format version; a warning
        /// is logged in the latter two cases (R10.9.5).
        /// </summary>
        public static bool TryLoad(string levelKey, string categoryKey, out MarkerSet set)
        {
            set = null;
            string path = SetPath(levelKey, categoryKey);
            if (!File.Exists(path))
                return false;
            try
            {
                string text = File.ReadAllText(path, new UTF8Encoding(false));
                string error;
                var warnings = new List<string>();
                if (!MarkerJson.TryParse(text, out set, out error, warnings))
                {
                    Plugin.Logger.LogWarning($"HSRTimer: markers '{path}' could not be read ({error}); treated as empty.");
                    set = null;
                    return false;
                }
                foreach (var w in warnings)
                    Plugin.Logger.LogWarning($"HSRTimer: markers '{path}': {w}");
                Normalize(set);
                if (string.IsNullOrEmpty(set.level_id))
                    set.level_id = levelKey ?? "";
                if (string.IsNullOrEmpty(set.category_key))
                    set.category_key = categoryKey ?? "Any";
                LogDebug($"loaded '{path}' (markers={set.markers.Count}, pbTimes={set.pbTimes.Count}, pb={(set.pb != null ? "yes" : "no")})");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: failed to parse markers '{path}': {ex.Message}");
                set = null;
                return false;
            }
        }

        /// <summary>
        /// Atomically write a marker set (temp + rename, R10.3.4). On failure the
        /// old file is preserved and a warning is logged.
        /// </summary>
        public static bool Save(MarkerSet set) => Save(set, onlyIfImproved: false);

        internal static bool Save(MarkerSet set, bool onlyIfImproved)
        {
            if (set == null)
                return false;
            try
            {
                set.format_version = FormatVersion;
                set.hsrtimer_version = PluginInfo.RuntimeVersion;
                set.updated_at = UtcNow();
                Normalize(set);
                string path = SetPath(set.level_id, set.category_key);
#if STORE_IL2CPP
                string lockPath = path + ".lock";
                StoreDataPolicy.EnsureSafePath(PersistenceService.PluginDir, path);
                StoreDataPolicy.EnsureSafePath(PersistenceService.PluginDir, lockPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using var lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                if (File.Exists(path))
                {
                    var warnings = new List<string>();
                    string prior = File.ReadAllText(path);
                    using var document = System.Text.Json.JsonDocument.Parse(prior);
                    var data = document.RootElement;
                    if (!data.TryGetProperty("markers", out var definitions) ||
                        definitions.ValueKind != System.Text.Json.JsonValueKind.Array ||
                        (data.TryGetProperty("pb", out var pb) && pb.ValueKind != System.Text.Json.JsonValueKind.Null
                         && (pb.ValueKind != System.Text.Json.JsonValueKind.Object
                             || !pb.TryGetProperty("total_ms", out var time)
                             || time.ValueKind != System.Text.Json.JsonValueKind.Number
                             || !time.TryGetInt64(out _))))
                        throw new IOException("Existing marker data has invalid fields and was retained.");
                    if (!MarkerJson.TryParse(prior, out var existing, out var error, warnings)
                        || warnings.Count > 0)
                        throw new IOException($"Existing marker data is unreadable and was retained: {error ?? warnings[0]}");
                    if (onlyIfImproved && existing.pb != null && existing.pb.total_ms != 0
                        && existing.pb.total_ms <= set.pb.total_ms)
                        return false;
                }
#endif
                string json = MarkerJson.Write(set);
                // A save that carries no markers at all is legitimate (a level
                // with every marker deleted), but it is also the fingerprint of
                // a writer bug — keep it visible when debugging is on.
                LogDebug($"saving '{path}' (markers={set.markers.Count}, pbTimes={set.pbTimes.Count}, bytes={json.Length})");
#if STORE_IL2CPP
                bool ok = SubsegmentFileStore.WriteAtomic(path, json,
                    keepBackup: !onlyIfImproved || ConfigService.Instance?.Settings?.AutomaticPbBackup == true);
#else
                bool ok = SubsegmentFileStore.WriteAtomic(path, json);
#endif
                if (!ok)
                    Plugin.Logger.LogWarning($"HSRTimer: markers save failed for '{path}' (see the write warning above).");
                return ok;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: failed to save markers: {ex.Message}");
                return false;
            }
        }

        /// <summary>Never leave null collections behind (they round-trip badly and break iteration).</summary>
        private static void Normalize(MarkerSet set)
        {
            if (set.markers == null)
                set.markers = new List<MarkerDef>();
            else
                set.markers.RemoveAll(m => m == null);
            if (set.pbTimes == null)
                set.pbTimes = new List<MarkerPbEntry>();
            else
                set.pbTimes.RemoveAll(e => e == null);
        }

        private static void LogDebug(string message)
        {
            var cfg = ConfigService.Instance;
            if (cfg != null && cfg.Settings != null && cfg.Settings.MarkersDebugLogging)
                Plugin.Logger.LogInfo("HSRTimer[markers]: " + message);
        }

        /// <summary>Whether a marker set file already exists for the level.</summary>
        public static bool Exists(string levelKey, string categoryKey)
            => File.Exists(SetPath(levelKey, categoryKey));

        private static string UtcNow()
            => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }
}
