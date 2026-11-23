#if STORE_IL2CPP
using System;
using System.IO;

namespace HSRTimer
{
    internal static class StoreDataPolicy
    {
#if STORE_PREVIEW
        internal static bool PbWritesEnabled => false;
#else
        internal static bool PbWritesEnabled => true;
#endif

        internal const string DataDirectory = "HSRTimer-MicrosoftStore";

        internal static string DataRoot { get; private set; }

        internal static void InitialiseDataRoot(string documentsPath, string localAppDataPath)
        {
            string fallback = string.IsNullOrEmpty(localAppDataPath) ? null : Path.Combine(localAppDataPath, DataDirectory);
            DataRoot = SelectDataRoot(documentsPath, fallback, Plugin.Logger.LogWarning);
            Plugin.Logger.LogInfo($"HSRTimer: Store data directory is writable: '{DataRoot}'.");
            if (!string.IsNullOrEmpty(fallback) && Directory.Exists(fallback)
                && !string.Equals(DataRoot, fallback, StringComparison.OrdinalIgnoreCase))
                Plugin.Logger.LogInfo($"HSRTimer: Documents is active. Existing LocalAppData files at '{fallback}' are available for manual copying.");
        }

        internal static string SelectDataRoot(string documentsPath, string fallback, Action<string> warn)
        {
            string preferred = string.IsNullOrEmpty(documentsPath) ? null : Path.Combine(documentsPath, DataDirectory);
            try
            {
                if (preferred == null || !Directory.Exists(documentsPath))
                    throw new DirectoryNotFoundException("Windows Documents could not be found.");
                ProbeWritable(preferred);
                return preferred;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            {
                if (string.IsNullOrEmpty(fallback))
                    throw new IOException("LocalAppData could not be located and Documents is unavailable.", ex);

                warn($"HSRTimer: cannot use Documents data directory '{preferred ?? "(unknown)"}': {ex.Message}. Trying '{fallback}'.");
                try { ProbeWritable(fallback); }
                catch (Exception fallbackFailure) when (fallbackFailure is IOException || fallbackFailure is UnauthorizedAccessException || fallbackFailure is System.Security.SecurityException)
                {
                    throw new IOException($"The fallback Store data directory '{fallback}' is also unwritable.", fallbackFailure);
                }
                return fallback;
            }
        }

        private static void ProbeWritable(string directory)
        {
            string probe = Path.Combine(directory, ".write-test-" + Guid.NewGuid().ToString("N"));
            EnsureSafePath(directory, probe);
            Directory.CreateDirectory(directory);
            using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            stream.WriteByte(0);
            stream.Flush(flushToDisk: true);
        }

        internal static string ResolveRecordPath(string dataDirectory, string configuredPath, string fallback)
        {
            try
            {
                if (!Path.IsPathRooted(configuredPath))
                {
                    string candidate = Path.GetFullPath(configuredPath, dataDirectory);
                    if (IsInside(dataDirectory, candidate))
                        return candidate;
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // Paths copied from another installation may be invalid here.
            }
            return Path.Combine(dataDirectory, fallback);
        }

        private static bool IsInside(string dataDirectory, string path)
        {
            string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory)) + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return path.Length > prefix.Length && path.StartsWith(prefix, comparison);
        }

        internal static void EnsureSafePath(string dataDirectory, string path)
        {
            string current = Path.GetFullPath(path);
            if (!IsInside(dataDirectory, current))
                throw new IOException("The Store write path must be inside " + dataDirectory);

            // Reject links/junctions, including ancestors of the Store root.
            // An otherwise isolated path must not redirect writes into Steam.
            while (current != null)
            {
                if (string.Equals(Path.GetFileName(current), "WindowsApps", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Store data cannot be written inside WindowsApps: " + current);
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Store write path crosses a reparse point (Windows-managed redirection or link): " + current);
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                current = Path.GetDirectoryName(current);
            }
        }
    }
}
#endif
