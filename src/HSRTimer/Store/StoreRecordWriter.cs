#if STORE_IL2CPP
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HSRTimer
{
    internal static class StoreRecordWriter
    {
        internal static void WriteFile(string root, string path, string contents, bool keepBackup)
        {
            StoreDataPolicy.EnsureSafePath(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(contents);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(path))
                    File.Replace(temp, path, keepBackup ? path + ".bak-" + Guid.NewGuid().ToString("N") : null);
                else
                    File.Move(temp, path);
            }
            finally
            {
                // Only remove the staging file created by this invocation.
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }

        internal static string ReadBundleDirectory(string directory)
        {
            string previous = directory + ".previous";
            if (Directory.Exists(directory)) return directory;
            string rollback = directory + ".rollback";
            if (Directory.Exists(rollback)) return rollback;
            return Directory.Exists(previous) ? previous : directory;
        }

        internal static bool TrySaveBetter(string root, string directory, long totalMs, string metadata,
            IReadOnlyDictionary<string, string> samples, bool keepBackup)
        {
            try
            {
                string previous = directory + ".previous";
                string rollback = directory + ".rollback";
                string stage = directory + ".tmp-" + Guid.NewGuid().ToString("N");
                string lockPath = directory + ".lock";
                StoreDataPolicy.EnsureSafePath(root, directory);
                StoreDataPolicy.EnsureSafePath(root, previous);
                StoreDataPolicy.EnsureSafePath(root, rollback);
                StoreDataPolicy.EnsureSafePath(root, lockPath);
                Directory.CreateDirectory(Path.GetDirectoryName(directory));

                using var lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                if (Directory.Exists(rollback))
                {
                    if (Directory.Exists(directory))
                        Directory.Delete(rollback, recursive: true);
                    else
                        Directory.Move(rollback, directory);
                }
                if (keepBackup && !Directory.Exists(directory) && Directory.Exists(previous))
                    Directory.Move(previous, directory);

                string existingDirectory = ReadBundleDirectory(directory);

                if (Directory.Exists(existingDirectory))
                {
                    if (!TryReadExisting(root, existingDirectory, out long existing))
                        throw new IOException("Existing PB is unreadable or incomplete. It was retained.");
                    if (existing <= totalMs)
                        return false;
                }

                try
                {
                    foreach (var sample in samples)
                        WriteFile(stage, Path.Combine(stage, sample.Key), sample.Value, keepBackup: false);
                    WriteFile(stage, Path.Combine(stage, "meta.json"), metadata, keepBackup: false);

                    string displaced = keepBackup ? previous : rollback;
                    if (Directory.Exists(directory))
                    {
                        if (keepBackup && Directory.Exists(previous))
                            Directory.Move(previous, directory + ".backup-" + Guid.NewGuid().ToString("N"));
                        Directory.Move(directory, displaced);
                    }

                    try { Directory.Move(stage, directory); }
                    catch
                    {
                        if (!Directory.Exists(directory) && Directory.Exists(displaced))
                            Directory.Move(displaced, directory);
                        throw;
                    }

                    if (!keepBackup && Directory.Exists(rollback))
                    {
                        try { Directory.Delete(rollback, recursive: true); }
                        catch (Exception ex)
                        {
                            Plugin.Logger.LogWarning($"HSRTimer: PB was saved, but temporary rollback cleanup failed: {ex.Message}");
                        }
                    }

                    return true;
                }
                finally
                {
                    if (Directory.Exists(stage))
                        Directory.Delete(stage, recursive: true);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: could not save PB '{directory}': {ex.Message}");
                return false;
            }
        }

        private static bool TryReadExisting(string root, string directory, out long totalMs)
        {
            totalMs = 0;
            string path = Path.Combine(directory, "meta.json");
            StoreDataPolicy.EnsureSafePath(root, path);
            if (!SubsegmentFileStore.TryLoadMeta(path, out var meta) || meta.total_ms < 0 || meta.sample_count <= 0)
                return false;

            string[] paths;
            if (meta.project == "IL")
                paths = new[] { Path.Combine(directory, "sample.jsonl") };
            else if (meta.project == "ML" && meta.level_ids != null && meta.level_ids.Length > 0)
                paths = Array.ConvertAll(meta.level_ids, id => Path.Combine(directory, "levels", id + ".jsonl"));
            else
                return false;

            int count = 0;
            foreach (string sample in paths)
            {
                StoreDataPolicy.EnsureSafePath(directory, sample);
                if (!SubsegmentFileStore.TryLoadSamples(sample, out var rows, strict: true))
                    return false;
                count += rows.Count;
            }
            totalMs = meta.total_ms;
            return count == meta.sample_count;
        }
    }
}
#endif
