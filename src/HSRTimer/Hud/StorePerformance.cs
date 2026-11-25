#if STORE_IL2CPP
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace HSRTimer
{
    internal static class StorePerformance
    {
        private const int MaxRows = 360;
        private const string FileName = "timer-performance.csv";
        private const string Header = "utc,uptime_s,scene,hud_visible,frames,percentile_frames,fps,mean_ms,max_ms,p95_ms,p99_ms,low_1pct_fps,"
            + "working_set_bytes,private_bytes,process_cpu_ms,managed_bytes,gc0,gc1,gc2,unity_allocated_bytes,"
            + "font_source,font_id,atlas_id,atlas_width,atlas_height,observed_atlas_changes,bundle_count,"
            + "font_checks,font_check_total_ms,hud_draws,hud_draw_total_ms,pb_transactions,pb_total_ms,collect_ms";
        private static StoreFrameWindow _window;
        private static Queue<string> _rows;
        private static double _deadline;
        private static readonly long[] Ticks = new long[3];
        private static readonly int[] Counts = new int[3];
        private static int? _lastAtlas, _lastWidth, _lastHeight, _lastFont;
        private static int _atlasChanges;
        private static string _status;
        private static MethodInfo _unityMemory;
        private static bool _checkedMemory;
        internal static bool Enabled { get; private set; }
        internal static int RetainedSamples => _rows?.Count ?? 0;

        internal readonly struct Measurement : IDisposable
        {
            private readonly long _start;
            private readonly int _channel;
            internal Measurement(int channel)
            {
                _channel = channel;
                _start = Enabled ? Stopwatch.GetTimestamp() : 0;
            }
            public void Dispose()
            {
                if (_start == 0) return;
                Ticks[_channel] += Stopwatch.GetTimestamp() - _start;
                Counts[_channel]++;
            }
        }

        internal static void Draw(GUIStyle button, GUIStyle label)
        {
            if (!ConfigService.DebugModeEnabled) return;
            if (GUILayout.Button(Enabled ? "Stop performance capture" : "Start performance capture", button))
            {
                if (Enabled)
                {
                    Enabled = false;
                    _status = "Performance capture stopped.";
                }
                else Start(Time.realtimeSinceStartup);
            }
            if (_rows?.Count > 0 && GUILayout.Button("Export timer-performance.csv", button))
            {
                try
                {
                    StoreRecordWriter.WriteFile(PersistenceService.PluginDir, PersistenceService.PathFor(FileName),
                        Header + "\n" + string.Join("\n", _rows) + "\n", keepBackup: false);
                    _status = FileName + " saved in the HSRTimer data folder.";
                }
                catch (Exception error) { _status = error.Message; }
            }
            if (_status != null) GUILayout.Label(_status, label);
        }

        internal static void Start(double now)
        {
            _window ??= new StoreFrameWindow(4096);
            _rows ??= new Queue<string>(MaxRows);
            _window.Reset();
            _rows.Clear();
            Array.Clear(Ticks, 0, Ticks.Length);
            Array.Clear(Counts, 0, Counts.Length);
            _lastAtlas = _lastWidth = _lastHeight = _lastFont = null;
            _atlasChanges = 0;
            _deadline = now + 5;
            _status = "Performance capture active (5-second samples).";
            Enabled = true;
        }

        internal static void Frame(bool hudVisible)
        {
            if (!Enabled) return;
            float now = Time.realtimeSinceStartup;
            _window.Add(Time.unscaledDeltaTime);
            if (now < _deadline) return;
            _deadline = now + 5;
            try { Capture(now, hudVisible); }
            catch (Exception error)
            {
                Enabled = false;
                _status = "Performance capture stopped: " + error.Message;
                Plugin.Logger.LogWarning(_status);
            }
        }

        private static void Capture(float now, bool hudVisible)
        {
            long start = Stopwatch.GetTimestamp();
            var summary = _window.Summarize();
            var font = StoreFontDiagnostics.PerformanceSnapshot();
            if (font.FontId == _lastFont && _lastAtlas.HasValue && font.AtlasId.HasValue
                && (font.AtlasId != _lastAtlas || font.Width != _lastWidth || font.Height != _lastHeight))
                _atlasChanges++;
            (_lastFont, _lastAtlas, _lastWidth, _lastHeight) = (font.FontId, font.AtlasId, font.Width, font.Height);
            long? rss = null, privateBytes = null;
            double? cpu = null;
            try
            {
                using var process = Process.GetCurrentProcess();
                rss = process.WorkingSet64;
                privateBytes = process.PrivateMemorySize64;
                cpu = process.TotalProcessorTime.TotalMilliseconds;
            }
            catch (Exception) { }
            string scene;
            try { scene = Application.loadedLevelName; }
            catch (Exception) { scene = ""; }

            var fields = new object[] { DateTime.UtcNow.ToString("O"), now, scene, hudVisible, summary.Frames, summary.PercentileFrames,
                summary.Fps, summary.MeanMs, summary.MaxMs, summary.P95Ms, summary.P99Ms, summary.LowFps,
                rss, privateBytes, cpu, GC.GetTotalMemory(false), GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
                UnityMemory(), font.Source, font.FontId, font.AtlasId, font.Width, font.Height, _atlasChanges, font.Bundles,
                Counts[0], Milliseconds(Ticks[0]), Counts[1], Milliseconds(Ticks[1]), Counts[2], Milliseconds(Ticks[2]) };
            var csv = Array.ConvertAll(fields, Format);
            if (_rows.Count == MaxRows) _rows.Dequeue();
            _rows.Enqueue(string.Join(",", csv) + "," + Format(Milliseconds(Stopwatch.GetTimestamp() - start)));
            _window.Reset();
            Array.Clear(Ticks, 0, Ticks.Length);
            Array.Clear(Counts, 0, Counts.Length);
            _status = "Performance capture active. Samples retained: " + _rows.Count;
        }

        private static long? UnityMemory()
        {
            try
            {
                if (!_checkedMemory)
                {
                    _checkedMemory = true;
                    _unityMemory = typeof(UnityEngine.Object).Assembly.GetType("UnityEngine.Profiling.Profiler")
                        ?.GetMethod("GetTotalAllocatedMemoryLong", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
                }
                return _unityMemory?.Invoke(null, null) as long?;
            }
            catch (Exception) { _unityMemory = null; return null; }
        }

        private static double Milliseconds(long ticks) => ticks * (1000d / Stopwatch.Frequency);

        private static string Format(object value) => value is string text
            ? "\"" + text.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\""
            : Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}
#endif
