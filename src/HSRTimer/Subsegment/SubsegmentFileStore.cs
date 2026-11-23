using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
#if STORE_IL2CPP
using System.Text.Json;
#else
using UnityEngine;
#endif

namespace HSRTimer
{
    /// <summary>
    /// File I/O for subsegment samples (R8.2 / R8.3.3). The loader is strict
    /// about rejecting malformed/missing files, but the failures are contained
    /// so the timer and any other loaded references are never affected.
    /// </summary>
    public static class SubsegmentFileStore
    {
        public const int FormatVersion = 1;

#if STORE_IL2CPP
        // These models are managed .NET objects. Unity's native JsonUtility
        // cannot serialize them in an IL2CPP player.
        private static readonly JsonSerializerOptions Fields = new JsonSerializerOptions { IncludeFields = true, IgnoreReadOnlyProperties = true };
        private static readonly JsonSerializerOptions IndentedFields = new JsonSerializerOptions(Fields) { WriteIndented = true };
#endif

        internal static string CategoryKey(IEnumerable<string> tags)
        {
            var sorted = tags == null ? new List<string>() : new List<string>(tags);
            if (sorted.Count == 0) return "Any";
            sorted.Sort(StringComparer.Ordinal);
            return string.Join("+", sorted);
        }

        public static string SanitizeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "_";
            var sb = new StringBuilder(id.Length);
            foreach (var c in id)
                sb.Append((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-' ? c : '_');
            return sb.ToString();
        }

        public static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (Path.IsPathRooted(path)) return path;
            return Path.Combine(PersistenceService.PluginDir, path);
        }

        /// <summary>Write a text file atomically: temp file + rename (R8.2.7.3).</summary>
        public static bool WriteAtomic(string path, string content) => WriteAtomic(path, content, keepBackup: true);

        internal static bool WriteAtomic(string path, string content, bool keepBackup)
        {
            try
            {
#if STORE_IL2CPP
                StoreRecordWriter.WriteFile(PersistenceService.PluginDir, path, content, keepBackup);
#else
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(tmp, path, null);
                    }
                    catch
                    {
                        // Some Mono/Unity setups do not implement File.Replace on
                        // the target filesystem. Fall back to the closest safe
                        // replacement (delete + move).
                        File.Delete(path);
                        File.Move(tmp, path);
                    }
                }
                else
                {
                    File.Move(tmp, path);
                }
#endif
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: subsegment atomic write failed for {path}: {ex.Message}");
#if !STORE_IL2CPP
                try { if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp"); } catch { }
#endif
                return false;
            }
        }

        public static bool TryLoadMeta(string path, out SubsegmentMeta meta)
        {
            meta = null;
            if (!File.Exists(path))
                return false;
            try
            {
#if STORE_IL2CPP
                using var document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
                var data = document.RootElement;
                if (!data.TryGetProperty("format_version", out _) || !data.TryGetProperty("total_ms", out _))
                    throw new InvalidDataException("Missing PB metadata fields.");
                meta = data.Deserialize<SubsegmentMeta>(Fields);
#else
                meta = JsonUtility.FromJson<SubsegmentMeta>(File.ReadAllText(path, Encoding.UTF8));
#endif
                if (meta == null || meta.format_version != FormatVersion)
                {
                    Plugin.Logger.LogWarning($"HSRTimer: subsegment meta '{path}' missing or unsupported format_version; skipped.");
                    meta = null;
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: failed to parse meta '{path}': {ex.Message}");
                meta = null;
                return false;
            }
        }

        public static string MakeMetaJson(
            string project,
            string levelId,
            string subproject,
            string categoryKey,
            string[] levelIds,
            long totalMs,
            int sampleCount)
        {
            var meta = new SubsegmentMeta
            {
                format_version = FormatVersion,
                project = project,
                level_id = levelId,
                subproject = subproject,
                category_key = categoryKey,
                level_ids = levelIds,
                total_ms = totalMs,
                sample_count = sampleCount,
                hsrtimer_version = PluginInfo.RuntimeVersion,
                created_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            };
#if STORE_IL2CPP
            return JsonSerializer.Serialize(meta, IndentedFields);
#else
            return JsonUtility.ToJson(meta, true);
#endif
        }

        public static string MakeSampleJson(IEnumerable<SubsegmentSample> samples)
        {
            var sb = new StringBuilder();
            foreach (var s in samples)
            {
                if (sb.Length > 0) sb.Append('\n');
#if STORE_IL2CPP
                sb.Append(JsonSerializer.Serialize(s, Fields));
#else
                sb.Append(JsonUtility.ToJson(s));
#endif
            }
            sb.Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// Load one JSONL sample file into an ordered list. A single malformed
        /// line is skipped with a warning naming the file and line number; the
        /// rest of the file is still used (R8.2.7.1).
        /// </summary>
        public static bool TryLoadSamples(string path, out List<SubsegmentSample> samples)
            => TryLoadSamples(path, out samples, strict: false);

        internal static bool TryLoadSamples(string path, out List<SubsegmentSample> samples, bool strict)
        {
            samples = new List<SubsegmentSample>();
            if (!File.Exists(path)) return false;
            try
            {
                string[] lines = File.ReadAllLines(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0) continue;
                    try
                    {
#if STORE_IL2CPP
                        var sample = JsonSerializer.Deserialize<SubsegmentSample>(line, Fields);
#else
                        var sample = JsonUtility.FromJson<SubsegmentSample>(line);
#endif
                        if (sample != null)
                            samples.Add(sample);
                        else if (strict)
                            return false;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"HSRTimer: subsegment {path}({i + 1}): bad sample line: {ex.Message}");
                        if (strict) return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: failed to read subsegment samples '{path}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Try to read an existing PB's total_ms so a new PB is only written when
        /// it actually beats it (strictly less than, per R8.3.1.2 / R8.3.2.5).
        /// </summary>
        public static bool TryReadTotalMs(string metaPath, out long totalMs)
        {
            totalMs = 0;
            if (!TryLoadMeta(metaPath, out var meta) || meta == null)
                return false;
            totalMs = meta.total_ms;
            return true;
        }
    }
}
