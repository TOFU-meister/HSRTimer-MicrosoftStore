#if STORE_IL2CPP
using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace HSRTimer
{
    // Accessed only from IMGUI on the Unity main thread. Nothing is persisted.
    internal static class StoreFontDiagnostics
    {
        private const string Sample = "0123456789\nABCDEFGHIJKLMNOPQRSTUVWXYZ\nabcdefghijklmnopqrstuvwxyz\n"
            + "00:00.000  01:23.456\n-00:01.234  +00:02.345\n"
            + "日本語 あいうえお アイウエオ 記録 更新\n简体中文 繁體中文 计时 记录\n!?%+-:.,/()[]\n"
            + "IIII WWWW\nGame Time: 00:00.000\n11:11.111  88:88.888";
        private static readonly string[] StyleSamples =
        {
            "0123456789", "IIII WWWW", "Game Time: 00:00.000", "11:11.111", "88:88.888",
            "日本語 あいうえお アイウエオ 記録 更新", "简体中文 繁體中文 计时 记录",
        };
        private static readonly string[] HudModes = { "Normal (font's own weight)", "Legacy HUD (Bold)" };
        private static int _hudMode;
        private static bool _captureHudStyle;
        private static FontStyle _appliedStyle;

        internal static FontStyle ActiveStyle => _applied != null ? _appliedStyle : _savedStyle;
        internal static bool IsSessionApplied => _applied != null;
        internal static bool CaptureHudStyle => _captureHudStyle;

        internal static void RecordHudStyles(string phase, GUIStyle row, GUIStyle banner)
        {
            try
            {
                RecordStyle(phase + " row", row);
                RecordStyle(phase + " banner", banner);
            }
            catch (Exception error) { Record("HUD style capture failed: " + Describe(error)); }
            if (phase == "HUD after") _captureHudStyle = false;
        }

        private static void RecordStyle(string phase, GUIStyle style)
        {
            var font = style.font ?? GUI.skin.font;
            var matrix = GUI.matrix;
            Record($"{phase} | font={font?.name ?? "unknown"} #{font?.GetInstanceID()} | style={style.fontStyle} "
                + $"| size={style.fontSize} | richText={style.richText} | matrix linear=[{matrix.m00},{matrix.m01};{matrix.m10},{matrix.m11}]");
            foreach (string sample in StyleSamples)
            {
                var size = style.CalcSize(new GUIContent(sample));
                float glyphWidth = 0;
                foreach (char glyph in sample)
                    glyphWidth += style.CalcSize(new GUIContent(glyph.ToString())).x;
                Record($"{phase} | {sample} | CalcSize={size.x:0.##}x{size.y:0.##} | sum of individual widths={glyphWidth:0.##}");
            }
        }

        private static void CompareStyles(Font font)
        {
            try
            {
                var style = new GUIStyle { font = font, fontSize = _size, richText = false, wordWrap = false };
                try
                {
                    RecordStyle("Preview Normal", style);
                    style.fontStyle = FontStyle.Bold;
                    RecordStyle("Preview Bold", style);
                }
                finally { style.font = null; }
            }
            catch (Exception error) { Record("Preview style capture failed: " + Describe(error)); }
            _captureHudStyle = true;
        }

        private static readonly string[] Families =
        {
            "PingFang SC", "Microsoft YaHei", "Noto Sans CJK SC", "Noto Sans CJK",
            "Heiti SC", "Arial Unicode MS", "Arial",
        };
        private sealed record Candidate(Font Font, string Source, string Name, int Id, bool Owned, string Kind, string Style, string BundlePath = null, string AssetName = null);
        private static readonly List<Candidate> Candidates = new();
        private static readonly List<string> OsNames = new();
        private sealed class BundleSlot
        {
            internal StoreFontBundle Bundle;
            internal StoreFontBundle.Route Route;
            internal long RetryAt;
            internal long HealthySince = -1;
            internal int Attempts;
            internal bool DiagnosticOwner;
            internal bool AwaitingHudDetach;
            internal bool DropAfterHudDetach;
        }

        private const int MaxRecoveryAttempts = 3;
        private const long RecoveryDelayMs = 2000;
        private static readonly Dictionary<string, BundleSlot> Bundles = new(StringComparer.OrdinalIgnoreCase);
        private static long _nextHealthCheck;
        private static bool _hudDetachPending;
        private static (string Path, string Asset)? _pendingHud, _pendingPreview;
        internal static Action DetachHudStyles { private get; set; }
        private static readonly Queue<string> History = new();
        private static string _search = "";
        private static string _bundleResult;
        private static Vector2 _osScroll, _candidateScroll;
        private static readonly HashSet<string> Warnings = new();
        private static int _selected, _osSelected;
        private static int _size = 18;
        private static bool _open, _bold, _previewed, _confirmed, _drawFailed, _defaultDrawFailed;
        private static Candidate _applied, _saved, _reportedSaved;
        private static string _savedPath, _savedAsset;
        private static FontStyle _savedStyle;
        private static Candidate Effective => _applied ?? _saved;
        private static GUIStyle _previewStyle;
        private static int _appliedSize;
        private static string _status = "Scan or probe to begin.";
        private static string _evidence;

        internal readonly record struct PerformanceFont(string Source, int? FontId, int? AtlasId, int? Width, int? Height, int Bundles);

        internal static PerformanceFont PerformanceSnapshot()
        {
            var candidate = Effective;
            int bundles = Bundles.Count - CountPendingBundles();
            if (candidate?.Font == null) return new PerformanceFont("IMGUI", null, null, null, null, bundles);
            try
            {
                var material = candidate.Font.material;
                var atlas = material == null ? null : material.mainTexture;
                if (atlas != null)
                    return new PerformanceFont(candidate.Source, candidate.Id, atlas.GetInstanceID(), atlas.width, atlas.height, bundles);
            }
            catch (Exception) { }
            return new PerformanceFont(candidate.Source, candidate.Id, null, null, null, bundles);
        }

        internal static Font ActiveFont()
        {
            var candidate = Effective;
            if (candidate == null) return null;
            if (candidate.Font != null) return candidate.Font;
            if (_applied != null)
                SuspendHud("The applied font was unloaded. HUD restored to the available default.");
            else
            {
                ClearSavedFont();
                SavedFontIssue = "HUD_FONT_UNAVAILABLE";
            }
            return _saved?.Font;
        }

        internal static int ActiveSize => _appliedSize;
        internal static int ResolveHudSize(int layoutSize) => IsSessionApplied ? ActiveSize : layoutSize;
        internal static bool IsApplied => Effective != null;

        internal static string CurrentFontName => Effective?.Name;
        internal static bool HudStyleChangePending => _hudDetachPending;
        internal static string SavedFontIssue { get; private set; }

        internal static string FontsDirectory(string dataRoot)
        {
            string path = Path.GetFullPath(Path.Combine(dataRoot, "fonts"));
            StoreDataPolicy.EnsureSafePath(dataRoot, path);
            return path;
        }

        internal static IReadOnlyList<string> ListFontBundles(string dataRoot)
        {
            string root = FontsDirectory(dataRoot);
            var names = new List<string>();
            if (!Directory.Exists(root)) return names;
            foreach (string path in Directory.EnumerateFiles(root))
            {
                string name = Path.GetFileName(path);
                if (!StoreHudFontPreference.TryExternal(name, "font", StoreHudFontStyle.Normal, out _)) continue;
                try { StoreDataPolicy.EnsureSafePath(root, path); }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                names.Add(name);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        internal sealed record FontChoice(string AssetName, string Name, Font Font);

        internal static IReadOnlyList<FontChoice> ReadFontChoices(string dataRoot, string name, out string newPath,
            Func<string, StoreFontBundle.Route, StoreFontBundle> load = null)
        {
            newPath = null;
            if (!StoreHudFontPreference.TryExternal(name, "font", StoreHudFontStyle.Normal, out _))
                throw new InvalidDataException("Invalid bundle filename.");
            string root = FontsDirectory(dataRoot);
            string path = Path.GetFullPath(Path.Combine(root, name));
            StoreDataPolicy.EnsureSafePath(root, path);
            if (!File.Exists(path)) throw new FileNotFoundException();

            bool found = Bundles.TryGetValue(path, out var slot);
            if ((found && slot.Bundle == null) || (!found && Bundles.Count >= StoreFontBundle.MaxBundles))
                throw new InvalidOperationException("Bundle busy or limit reached.");

            StoreFontBundle bundle = slot?.Bundle;
            if (!found)
            {
                StoreDataPolicy.EnsureSafePath(root, path);
                bundle = load == null ? StoreFontBundle.Load(path, StoreFontBundle.Route.File, _ => { })
                    : load(path, StoreFontBundle.Route.File);
            }

            var choices = new List<FontChoice>();
            bool accepted = false;
            try
            {
                if (Health(bundle) != null) throw new InvalidDataException("Unhealthy bundle.");
                for (int i = 0; i < bundle.Fonts.Count; i++)
                {
                    Font font = bundle.Fonts[i];
                    string asset = bundle.FontNames[i];
                    if (font != null && font.dynamic
                        && StoreHudFontPreference.TryExternal(name, asset, StoreHudFontStyle.Normal, out _))
                        choices.Add(new FontChoice(asset, font.name, font));
                }
                if (choices.Count == 0) throw new InvalidDataException("No usable Dynamic Font.");
                accepted = true;
            }
            finally
            {
                if (!found && !accepted) DisposeBundle(bundle);
            }
            if (!found)
            {
                RegisterBundle(path, StoreFontBundle.Route.File, bundle, selectNew: false);
                newPath = path;
            }
            return choices;
        }

        internal static void DropUnusedBundle(string path)
        {
            if (path == null || MatchesAsset(path, _savedPath) || MatchesAsset(path, _applied?.BundlePath)
                || (_pendingHud is { } pending && MatchesAsset(path, pending.Path))
                || !Bundles.TryGetValue(path, out var slot) || slot.DiagnosticOwner)
                return;
            if (slot.AwaitingHudDetach)
            {
                slot.DropAfterHudDetach = true;
                return;
            }
            Bundles.Remove(path);
            var selected = Candidates.Count > 0 ? Candidates[_selected] : null;
            Candidates.RemoveAll(candidate => MatchesAsset(candidate.BundlePath, path));
            if (_pendingPreview is { } preview && MatchesAsset(preview.Path, path)) _pendingPreview = null;
            Select(Math.Max(0, Candidates.IndexOf(selected)));
            DisposeBundle(slot.Bundle);
        }

        private static Candidate ResolveFontChoice(StoreHudFontPreference preference, string dataRoot, out string newPath,
            Func<string, StoreFontBundle.Route, StoreFontBundle> load = null)
        {
            ReadFontChoices(dataRoot, preference.BundleFileName, out newPath, load);
            string path = Path.Combine(FontsDirectory(dataRoot), preference.BundleFileName);
            return FindSavedCandidate(path, preference.AssetName)
                ?? throw new InvalidDataException("Saved asset is unavailable.");
        }

        internal static string FontIssue(Exception error) => error switch
        {
            FileNotFoundException => "HUD_FONT_MISSING",
            InvalidOperationException => "HUD_FONT_BUSY",
            _ => "HUD_FONT_UNAVAILABLE",
        };

        internal static void RestoreSavedFont(StoreHudFontPreference preference, string dataRoot,
            Func<string, StoreFontBundle.Route, StoreFontBundle> load = null)
        {
            if (!preference.IsExternal) return;
            string newPath = null;
            try
            {
                Record($"Restoring saved HUD font: {preference.BundleFileName} / {preference.AssetName} ({preference.Style}).");
                SetSavedFont(preference, ResolveFontChoice(preference, dataRoot, out newPath, load));
                Record($"Saved HUD font bundle {preference.BundleFileName} ready. Matched {_saved.AssetName} to {_saved.Name}.");
            }
            catch (Exception error)
            {
                DropUnusedBundle(newPath);
                SavedFontIssue = FontIssue(error);
                Record($"Saved HUD font {preference.BundleFileName}: {SavedFontIssue} ({error.GetType().Name}). Using IMGUI when no session font is active.");
            }
        }

        internal static string ApplyAndSaveFont(SettingsModel settings, StoreHudFontPreference preference, string dataRoot)
        {
            Candidate candidate = null;
            string newPath = null;
            try
            {
                try
                {
                    if (preference.IsExternal) candidate = ResolveFontChoice(preference, dataRoot, out newPath);
                }
                catch (Exception error) { return FontIssue(error); }

                if (!settings.TrySaveHudFont(preference)) return "HUD_FONT_SAVE_FAILED";

                // Commit managed state after the atomic INI write. Native style
                // detachment is best effort and cannot turn this into a save failure.
                string oldPath = _savedPath;
                ResetHud("Saved HUD font selection changed.");
                SetSavedFont(preference, candidate);
                DropUnusedBundle(oldPath);
                return null;
            }
            finally { DropUnusedBundle(newPath); }
        }

        private static void SetSavedFont(StoreHudFontPreference preference, Candidate candidate)
        {
            ClearSavedFont();
            _savedPath = candidate?.BundlePath;
            _savedAsset = candidate?.AssetName;
            _savedStyle = preference.Style == StoreHudFontStyle.Bold ? FontStyle.Bold : FontStyle.Normal;
            _saved = candidate;
            SavedFontIssue = null;
        }

        private static bool MatchesAsset(string actual, string expected)
            => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

        private static Candidate FindSavedCandidate(string path, string asset)
            => Candidates.Find(candidate => string.Equals(candidate.BundlePath, path, StringComparison.OrdinalIgnoreCase)
                && MatchesAsset(candidate.AssetName, asset) && candidate.Font != null && candidate.Font.dynamic);

        internal static void ReportSavedDraw(int size)
        {
            if (!ConfigService.DebugModeEnabled || _applied != null || _saved == null || _reportedSaved == _saved) return;
            _reportedSaved = _saved;
            Record($"Saved HUD font drawn: {_saved.Name} #{_saved.Id} / {_saved.Source} / {_saved.AssetName} "
                + $"at {size}px, style={_savedStyle}.");
        }

        private static void ClearSavedFont()
        {
            if (_saved == null) return;
            var previous = _saved;
            _saved = null;
            _reportedSaved = null;
            if (_applied == null) TryDetachHud(previous.BundlePath);
        }

        internal static void HudFailed(Exception error)
        {
            if (_applied == null)
            {
                ClearSavedFont();
                SavedFontIssue = "HUD_FONT_UNAVAILABLE";
                if (Warnings.Add("Saved Timer HUD")) Record($"Saved HUD font draw failed ({error.GetType().Name}). Using IMGUI.");
                return;
            }
            if (Candidates.Count > 0 && Candidates[_selected] == _applied)
            {
                _confirmed = false;
                _previewed = false;
            }
            string status = "Timer HUD fell back. " + Describe(error);
            if (_applied?.BundlePath is { } path && Bundles.TryGetValue(path, out var slot)
                && slot.Bundle != null && Health(slot.Bundle) != null)
                SuspendHud(status);
            else
                ResetHud(status);
            if (Warnings.Add("Timer HUD")) Plugin.Logger.LogWarning("[Font Diagnostics] " + _status);
        }

        internal static void Draw(GUIStyle button, GUIStyle label)
        {
            if (!ConfigService.DebugModeEnabled) return;
            if (GUILayout.Button(_open ? "Hide Font Diagnostics" : "Font Diagnostics", button))
            {
                _open = !_open;
                if (!_open) PanelTextInput.Cancel();
            }
            if (!_open) return;

            GUILayout.Label("Session only. The settings panel keeps its own font.", label);
            GUILayout.Label("IMGUI skin: " + NameOf(() => GUI.skin.font)
                + " | label override: " + NameOf(() => GUI.skin.label.font)
                + " | Timer HUD: " + (Effective?.Name ?? "IMGUI skin"), label);

            if (GUILayout.Button("Scan game fonts", button)) Scan();
            if (GUILayout.Button("Inspect interop and internal-call availability", button)) ProbeInterop();
            if (GUILayout.Button("Load local font bundles (file)", button)) LoadBundles(StoreFontBundle.Route.File);
            if (GUILayout.Button("Compare file (CRC 0, offset 0)", button)) LoadBundles(StoreFontBundle.Route.FileExplicit);
            if (GUILayout.Button("Compare memory", button)) LoadBundles(StoreFontBundle.Route.Memory);
            if (GUILayout.Button("Compare memory (CRC 0)", button)) LoadBundles(StoreFontBundle.Route.MemoryExplicit);
            if (_bundleResult != null) GUILayout.Label(_bundleResult, label);
            if (GUILayout.Button("Record current status", button)) RecordStatus();
            if (GUILayout.Button("Export diagnostic report", button)) ExportReport();
            if (GUILayout.Button(_saved == null ? "Restore IMGUI / release probe fonts" : "Restore saved HUD font / release probe fonts", button))
                Release(keepSaved: true);
            DrawOsFonts(button, label);
            GUILayout.Label(_status, label);

            if (Candidates.Count == 0) return;
            _candidateScroll = GUILayout.BeginScrollView(_candidateScroll, GUILayout.Height(112f));
            try
            {
                for (int i = 0; i < Candidates.Count; i++)
                    if (GUILayout.Button((i == _selected ? "▶ " : "") + Identify(Candidates[i]), button))
                    {
                        _pendingPreview = null;
                        Select(i);
                        Record("Selected " + Identify(Candidates[i]));
                    }
            }
            finally { GUILayout.EndScrollView(); }
            var candidate = Candidates[_selected];
            if (candidate.Font == null)
            {
                GUILayout.Label("Font unloaded. Scan again to find live objects.", label);
                return;
            }

            if (GUILayout.Button($"Preview size {_size} (click to cycle)", button))
            {
                _size = _size >= 26 ? 14 : _size + 4;
                Select(_selected);
            }
            bool bold = GUILayout.Toggle(_bold, "Bold preview");
            if (bold != _bold)
            {
                _bold = bold;
                Select(_selected);
            }
            if (GUILayout.Button("Inspect glyph and atlas APIs", button)) Inspect(candidate.Font);
            if (_evidence != null) GUILayout.Label(_evidence, label);

            if (GUILayout.Button($"HUD style on next apply: {HudModes[_hudMode]}", button))
                _hudMode = (_hudMode + 1) % HudModes.Length;
            if (GUILayout.Button("Record preview and HUD style comparison", button)) CompareStyles(candidate.Font);

            float height = (_size + 6) * 11;
            GUILayout.Label("IMGUI comparison", label);
            Preview(GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true)), null);
            GUILayout.Label("Candidate preview", label);
            if (Preview(GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true)), candidate.Font) && !_previewed)
            {
                _previewed = true;
                Record("Preview draw returned: " + Identify(candidate) + $", {_size}px, bold={_bold}; awaiting visual check.");
            }
            GUILayout.Label(_drawFailed ? "Draw call failed." : _confirmed ? "Visually checked by user."
                : _previewed ? "Draw call returned. Check for blank or missing glyphs." : "Waiting for repaint.", label);
            // Keep the control tree stable across IMGUI Layout and Repaint.
            if (GUILayout.Button("I checked the candidate visually", button) && _previewed && !_drawFailed)
            {
                _confirmed = true;
                Record("Visual check passed: " + Identify(candidate));
            }
            if (GUILayout.Button("The preview has missing or incorrect glyphs", button) && _previewed)
            {
                _confirmed = false;
                Record("Visual check failed: " + Identify(candidate));
            }
            if (GUILayout.Button("Apply to Timer HUD for this session", button) && _confirmed && !_drawFailed)
            {
                _pendingHud = null;
                _applied = candidate;
                _appliedSize = _size;
                _appliedStyle = _hudMode == 0 ? FontStyle.Normal : FontStyle.Bold;
                _captureHudStyle = true;
                Record("HUD applied: " + Identify(candidate) + $" at {_size}px, style={_appliedStyle} ({HudModes[_hudMode]})");
                _status = "Applied to the timer rows and warning banner. Check during play and after Retry.";
            }
        }

        private static void DrawOsFonts(GUIStyle button, GUIStyle label)
        {
            if (GUILayout.Button("List Unity OS fonts", button)) ListOsFonts();
            IReadOnlyList<string> names = OsNames.Count > 0 ? OsNames : Families;
            GUILayout.Label("Find OS font (Enter to apply the filter)", label);
            string query = PanelTextInput.Draw("store-font-search", _search, button);
            if (query != _search)
            {
                _search = query.Trim();
                _osScroll = Vector2.zero;
            }
            GUILayout.Label($"OS fonts: {OsNames.Count} retrieved, {CountMatches(names, _search)} shown"
                + (OsNames.Count == 0 ? " (upstream choices)" : ""), label);
            _osScroll = GUILayout.BeginScrollView(_osScroll, GUILayout.Height(135f));
            try
            {
                for (int i = 0; i < names.Count; i++)
                {
                    if (!Matches(names[i], _search)) continue;
                    if (GUILayout.Button((i == _osSelected ? "▶ " : "") + names[i], button))
                    {
                        _osSelected = i;
                        Record("Selected OS font: " + names[i]);
                    }
                }
            }
            finally { GUILayout.EndScrollView(); }

            string selectedName = names[_osSelected];
            GUILayout.Label($"OS font {_osSelected + 1}/{names.Count}: {selectedName}", label);
            if (GUILayout.Button("Probe selected OS font", button))
                Acquire("Unity single family", () => CreateSingle(selectedName, _size), true, "OS Font", selectedName);
            if (GUILayout.Button("Probe upstream fallback order", button))
                Acquire("Unity upstream font chain", () => CreateChain(_size), true, "OS Font");
            if (GUILayout.Button("Probe Font(string) with selected name", button))
                Acquire("Font(string)", () => CreateByName(selectedName), true, "OS Font", selectedName);
        }

        private static bool Matches(string name, string query)
            => name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private static int CountMatches(IReadOnlyList<string> names, string query)
        {
            int count = 0;
            foreach (var name in names)
                if (Matches(name, query)) count++;
            return count;
        }

        private static string Identify(Candidate candidate)
            => $"{candidate.Name} #{candidate.Id} — {candidate.Kind} / {candidate.Source} / {candidate.Style}";

        private static void Record(string message, bool log = true)
        {
            if (ConfigService.DebugModeEnabled)
            {
                if (History.Count >= 256) History.Dequeue();
                History.Enqueue(DateTime.UtcNow.ToString("O") + " " + message);
            }
            if (log) Plugin.Logger.LogInfo("[Font Diagnostics] " + message);
        }

        private static void RecordStatus()
        {
            Record("Status at " + Result(() => Application.loadedLevelName) + ": IMGUI=" + NameOf(() => GUI.skin.font)
                + "; OS font count=" + OsNames.Count + "; HUD=" + (Effective == null ? "IMGUI" : Identify(Effective)));
            Record($"Bundle slots={Bundles.Count}; recovery pending={CountPendingBundles()}.");
            foreach (var candidate in Candidates)
            {
                string state = candidate.Font != null ? "live" : "unloaded";
                if (candidate.BundlePath != null && Bundles.TryGetValue(candidate.BundlePath, out var slot) && slot.Bundle != null)
                {
                    int index = slot.Bundle.Fonts.IndexOf(candidate.Font);
                    if (index >= 0) state += "; " + Result(() => slot.Bundle.ResourceStatus(index));
                }
                Record(Identify(candidate) + " / " + state);
            }
        }

        private static void ExportReport()
        {
            try
            {
                RecordStatus();
                const string file = "font-diagnostics.txt";
                StoreRecordWriter.WriteFile(PersistenceService.PluginDir, PersistenceService.PathFor(file),
                    string.Join(Environment.NewLine, History) + Environment.NewLine, keepBackup: false);
                _status = "Saved " + file + " in the data folder.";
            }
            catch (Exception error) { Report("Export font diagnostics", error); }
        }

        private static void LoadBundles(StoreFontBundle.Route route)
        {
            string folder = PersistenceService.PathFor("fonts");
            _bundleResult = null;
            var details = new List<string>();
            void Note(string message)
            {
                details.Add(message);
                Record(message, log: false);
            }
            Note("Bundle test: " + StoreFontBundle.RouteName(route) + ", platform=" + Result(() => Application.platform.ToString()));
            try
            {
                StoreDataPolicy.EnsureSafePath(PersistenceService.PluginDir, folder);
                Directory.CreateDirectory(folder);
                int added = 0, attempts = 0;
                foreach (string path in Directory.EnumerateFiles(folder, "*.bundle"))
                {
                    if (attempts >= StoreFontBundle.MaxBundles) break;
                    if (Bundles.TryGetValue(path, out var current) && current.Bundle != null)
                    {
                        string reason = Health(current.Bundle);
                        if (reason == null)
                        {
                            current.DiagnosticOwner = true;
                            Note("Skipped " + Path.GetFileName(path) + ": already open. Restore IMGUI and release before retrying.");
                            continue;
                        }
                        if (!Invalidate(path, current, reason, Environment.TickCount64)) continue;
                    }
                    if (current == null && Bundles.Count >= StoreFontBundle.MaxBundles) continue;

                    attempts++;
                    try
                    {
                        StoreDataPolicy.EnsureSafePath(PersistenceService.PluginDir, path);
                        var bundle = LoadWithUnityMessages(path, route, Note);
                        RegisterBundle(path, route, bundle, selectNew: true);
                        added++;
                        Note($"Loaded {Path.GetFileName(path)} ({bundle.Fonts.Count} Font; {bundle.TextureCount} Texture2D).");
                    }
                    catch (Exception error) { Note("Load bundle " + Path.GetFileName(path) + ": " + StoreFontBundle.Describe(error)); }
                }
                Note($"Loaded {added} new bundle(s); {Bundles.Count - CountPendingBundles()}/{StoreFontBundle.MaxBundles} open. Folder: {folder}");
            }
            catch (Exception error) { Note("Load local font bundles: " + StoreFontBundle.Describe(error)); }
            _bundleResult = string.Join("\n", details);
        }

        private static int CountPendingBundles()
        {
            int count = 0;
            foreach (var slot in Bundles.Values)
                if (slot.Bundle == null) count++;
            return count;
        }

        internal static void RegisterBundle(string path, StoreFontBundle.Route route, StoreFontBundle bundle, bool selectNew, int recoveryAttempts = 0)
        {
            bool hasSlot = Bundles.TryGetValue(path, out var slot);
            if (slot != null && ReferenceEquals(slot.Bundle, bundle))
            {
                slot.DiagnosticOwner |= selectNew;
                return;
            }
            if (slot?.Bundle != null || (!hasSlot && Bundles.Count >= StoreFontBundle.MaxBundles))
            {
                DisposeBundle(bundle);
                throw new InvalidOperationException("The font bundle is already registered or all bundle slots are occupied.");
            }

            int before = Candidates.Count;
            try
            {
                for (int i = 0; i < bundle.Fonts.Count; i++)
                {
                    int index = Add(bundle.Fonts[i], Path.GetFileName(path), false, "AssetBundle", path, bundle.FontNames[i]);
                    if (selectNew || (_pendingPreview is { } preview && preview.Path == path && preview.Asset == bundle.FontNames[i]))
                        Select(index);
                }
                bool restoresSaved = MatchesAsset(path, _savedPath);
                var saved = restoresSaved ? FindSavedCandidate(path, _savedAsset) : null;
                if (_pendingHud is { } pending && MatchesAsset(pending.Path, path))
                {
                    var recovered = Candidates.Find(candidate => MatchesAsset(candidate.BundlePath, path) && MatchesAsset(candidate.AssetName, pending.Asset));
                    _pendingHud = null;
                    if (recovered?.Font != null)
                    {
                        _applied = recovered;
                        _status = "Recovered external Font and re-applied it to the Timer HUD.";
                        Record(_status + " " + Identify(recovered));
                    }
                    else
                    {
                        _status = "The previous HUD asset was absent after reloading. Using IMGUI.";
                        Record(_status);
                    }
                }
                if (restoresSaved)
                {
                    _saved = saved;
                    if (_saved != null) SavedFontIssue = null;
                    if (_saved == null)
                        Record($"Saved HUD font asset {_savedAsset} is unavailable after recovering {Path.GetFileName(path)}.");
                }
                if (_pendingPreview is { } selection && selection.Path == path) _pendingPreview = null;
                Bundles[path] = new BundleSlot
                {
                    Bundle = bundle, Route = route, Attempts = recoveryAttempts,
                    DiagnosticOwner = selectNew || slot?.DiagnosticOwner == true,
                };
            }
            catch
            {
                Candidates.RemoveRange(before, Candidates.Count - before);
                Select(0);
                DisposeBundle(bundle);
                throw;
            }
        }

        private static string Health(StoreFontBundle bundle)
        {
            try { return bundle.CheckHealth(); }
            catch (Exception error) { return StoreFontBundle.Describe(error); }
        }

        private static bool Invalidate(string path, BundleSlot slot, string reason, long now)
        {
            if (slot.AwaitingHudDetach) return false;
            _status = $"Invalidated at {Result(() => Application.loadedLevelName)}: {Path.GetFileName(path)} ({reason}).";
            Record(_status);
            if (ConfigService.DebugModeEnabled)
                foreach (var candidate in Candidates)
                    if (candidate.BundlePath == path) Record("Withdrawn candidate: " + Identify(candidate), log: false);
            Candidate selected = Candidates.Count > 0 ? Candidates[_selected] : null;
            if (selected?.BundlePath == path) _pendingPreview = (path, selected.AssetName);
            if (string.Equals(path, _savedPath, StringComparison.OrdinalIgnoreCase))
            {
                ClearSavedFont();
                SavedFontIssue = "HUD_FONT_UNAVAILABLE";
            }
            if (_applied?.BundlePath == path) SuspendHud("External font unavailable. Using IMGUI while recovering.");
            Candidates.RemoveAll(candidate => candidate.BundlePath == path);
            int remainingIndex = selected == null ? -1 : Candidates.IndexOf(selected);
            if (remainingIndex < 0) Select(0);
            else _selected = remainingIndex;
            if (slot.AwaitingHudDetach) return false;
            var bundle = slot.Bundle;
            slot.Bundle = null;
            slot.RetryAt = now + RecoveryDelayMs;
            DisposeBundle(bundle);
            return true;
        }

        private static void DisposeBundle(StoreFontBundle bundle)
        {
            try { bundle?.Dispose(); }
            catch (Exception error) { Report("Unload font bundle", error); }
        }

        internal static void Tick()
        {
            MaintainBundles(Environment.TickCount64, (path, route) =>
            {
                StoreDataPolicy.EnsureSafePath(Path.Combine(PersistenceService.PluginDir, "fonts"), path);
                return ConfigService.DebugModeEnabled
                    ? LoadWithUnityMessages(path, route, message => Record(message, log: false))
                    : StoreFontBundle.Load(path, route, _ => { });
            });
        }

        internal static void MaintainBundles(long now, Func<string, StoreFontBundle.Route, StoreFontBundle> load)
        {
            if (Bundles.Count == 0 || now < _nextHealthCheck) return;
            _nextHealthCheck = now + 500;
            using var measurement = new StorePerformance.Measurement(0);
            foreach (var entry in new List<KeyValuePair<string, BundleSlot>>(Bundles))
            {
                string path = entry.Key;
                var slot = entry.Value;
                if (slot.AwaitingHudDetach) continue;
                if (slot.Bundle != null)
                {
                    string reason = Health(slot.Bundle);
                    if (reason != null)
                    {
                        if (Invalidate(path, slot, reason, now) && slot.Attempts >= MaxRecoveryAttempts)
                            StopRecovery(path);
                    }
                    else if (slot.HealthySince < 0)
                        slot.HealthySince = now;
                    else if (now - slot.HealthySince >= 5000)
                        slot.Attempts = 0;
                    continue;
                }
                if (now < slot.RetryAt) continue;
                slot.Attempts++;
                Record($"Recovery {slot.Attempts}/{MaxRecoveryAttempts} at {Result(() => Application.loadedLevelName)}: {Path.GetFileName(path)}.");
                try
                {
                    RegisterBundle(path, slot.Route, load(path, slot.Route), selectNew: false, recoveryAttempts: slot.Attempts);
                    Record("Recovered " + Path.GetFileName(path) + ".");
                }
                catch (Exception error)
                {
                    string failure = string.Equals(path, _savedPath, StringComparison.OrdinalIgnoreCase)
                        ? error.GetType().Name : StoreFontBundle.Describe(error);
                    Record("Recovery failed: " + failure, log: false);
                    slot.RetryAt = now + RecoveryDelayMs;
                    if (slot.Attempts < MaxRecoveryAttempts) continue;
                    StopRecovery(path);
                }
            }
        }

        private static void StopRecovery(string path)
        {
            Bundles.Remove(path);
            if (_pendingHud is { } hud && hud.Path == path) _pendingHud = null;
            if (_pendingPreview is { } preview && preview.Path == path) _pendingPreview = null;
            _status = "External Font recovery stopped after 3 attempts. Load the bundle manually to try again.";
            Record(_status);
        }

        private static StoreFontBundle LoadWithUnityMessages(string path, StoreFontBundle.Route route, Action<string> note)
        {
            var messages = new List<string>();
            Action detach = null;
            try
            {
                try
                {
                    detach = ListenForUnityMessages((message, _, type) =>
                    {
                        if (messages.Count < 12 && type != LogType.Log)
                            messages.Add("Unity [" + type + "] " + message);
                    });
                }
                catch (Exception error) { note("Unity log hook unavailable: " + Describe(error)); }
                return StoreFontBundle.Load(path, route, note);
            }
            finally
            {
                try { detach?.Invoke(); }
                catch (Exception error) { note("Unity log hook removal failed: " + Describe(error)); }
                foreach (var message in messages) note(message);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static Action ListenForUnityMessages(Action<string, string, LogType> listener)
        {
            Application.LogCallback callback = listener;
            var signature = new[] { typeof(Application.LogCallback) };
            var add = typeof(Application).GetMethod("add_logMessageReceived", signature);
            var remove = typeof(Application).GetMethod("remove_logMessageReceived", signature);
            if (add == null || remove == null) throw new MissingMethodException("Application.logMessageReceived");
            add.Invoke(null, new object[] { callback });
            return () => remove.Invoke(null, new object[] { callback });
        }

        private static void ProbeInterop()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var type = typeof(Font);
            Record("Font interop: " + type.Assembly.FullName + "; MVID=" + type.Module.ModuleVersionId);
            foreach (var signature in new[] { Type.EmptyTypes, new[] { typeof(string) }, new[] { typeof(Il2CppStringArray), typeof(int) } })
                Record("Constructor (" + string.Join(", ", Array.ConvertAll(signature, t => t.Name)) + ") "
                    + (FindConstructor(signature) == null ? "missing" : "present"));
            foreach (string name in new[] { "CreateDynamicFontFromOSFont", "GetOSInstalledFontNames", "Internal_CreateFont",
                "Internal_CreateFontFromPath", "Internal_CreateDynamicFont" })
            {
                int count = 0;
                foreach (var method in type.GetMethods(flags))
                    if (method.Name == name) { count++; Record("Wrapper present: " + method); }
                if (count == 0) Record("Wrapper missing: " + name);
            }

            foreach (var line in ReadGameMetadata())
                Record(line);

            foreach (var name in new[] { "GetOSInstalledFontNames", "Internal_CreateFont", "Internal_CreateFontFromPath", "Internal_CreateDynamicFont" })
                Record("Internal call " + name + ": " + Result(() => ResolveInternalCall(name)));
            Record(Result(StoreFontBundle.ApiStatus));
            _status = "Interop and registration results recorded, with game metadata when available. Definitions alone do not establish callable constructors.";
        }

        private static List<string> ReadGameMetadata()
        {
            try { return EnumerateGameMethods(); }
            catch (Exception error)
            {
                return new List<string> { "Game metadata query unavailable: " + Describe(error) };
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static List<string> EnumerateGameMethods()
        {
            const int limit = 64;
            var result = new List<string>();
            var fontClass = IL2CPP.GetIl2CppClass("UnityEngine.TextRenderingModule.dll", "UnityEngine", "Font");
            if (fontClass == IntPtr.Zero)
            {
                result.Add("Game metadata query unavailable: UnityEngine.Font class not found.");
                return result;
            }

            string ClassName(IntPtr klass) => klass == IntPtr.Zero ? "<unavailable>"
                : IL2CPP.il2cpp_class_get_namespace_(klass) + "." + IL2CPP.il2cpp_class_get_name_(klass);
            string TypeName(IntPtr type) => type == IntPtr.Zero ? "<unknown>" : IL2CPP.il2cpp_type_get_name_(type) ?? "<unknown>";

            var image = IL2CPP.il2cpp_class_get_image(fontClass);
            result.Add("Game metadata class: " + ClassName(fontClass)
                + ", image=" + (image == IntPtr.Zero ? "<unavailable>" : IL2CPP.il2cpp_image_get_name_(image))
                + ", parent=" + ClassName(IL2CPP.il2cpp_class_get_parent(fontClass))
                + ", type=" + TypeName(IL2CPP.il2cpp_class_get_type(fontClass)));
            var liveFont = GUI.skin.font;
            var liveClass = liveFont == null ? IntPtr.Zero : IL2CPP.il2cpp_object_get_class(liveFont.Pointer);
            result.Add("Live IMGUI font class: " + (liveClass == IntPtr.Zero ? "unavailable"
                : ClassName(liveClass) + (liveClass == fontClass ? " (matches)" : " (differs)")));

            var observed = new HashSet<string>(StringComparer.Ordinal);
            IntPtr iterator = IntPtr.Zero;
            int count = 0;
            int constructors = 0;
            IntPtr method;
            while ((method = IL2CPP.il2cpp_class_get_methods(fontClass, ref iterator)) != IntPtr.Zero && count < limit)
            {
                count++;
                string name = IL2CPP.il2cpp_method_get_name_(method);
                if (name == ".ctor") constructors++;
                var parameters = new string[checked((int)IL2CPP.il2cpp_method_get_param_count(method))];
                for (int i = 0; i < parameters.Length; i++)
                    parameters[i] = TypeName(IL2CPP.il2cpp_method_get_param(method, (uint)i));

                string signature = name + "(" + string.Join(", ", parameters) + ")";
                observed.Add(signature);
                uint implementation = 0;
                uint flags = IL2CPP.il2cpp_method_get_flags(method, ref implementation);
                result.Add($"Game metadata: {signature}, token=0x{IL2CPP.il2cpp_method_get_token(method):X8},"
                    + $" argc={parameters.Length}, returns={TypeName(IL2CPP.il2cpp_method_get_return_type(method))},"
                    + $" {((flags & 0x10) != 0 ? "static" : "instance")}, impl=0x{implementation:X4}");
            }

            bool complete = method == IntPtr.Zero;
            result.Add($"Game metadata enumeration: {count} methods, {constructors} instance constructors"
                + (complete ? "; completed." : $"; truncated at {limit}."));
            bool fontControl = observed.Contains("get_dynamic()") && observed.Contains("HasCharacter(System.Char)");
            bool objectControl = HasGameMethod("UnityEngine.CoreModule.dll", "Object", "GetInstanceID");
            result.Add($"Controls: Font.get_dynamic / HasCharacter={fontControl}, Object.GetInstanceID={objectControl},"
                + $" Texture2D.get_whiteTexture={HasGameMethod("UnityEngine.CoreModule.dll", "Texture2D", "get_whiteTexture")},"
                + $" AssetBundle.GetAllAssetNames={HasGameMethod("UnityEngine.AssetBundleModule.dll", "AssetBundle", "GetAllAssetNames")}.");
            if (!complete || !fontControl || !objectControl || (liveClass != IntPtr.Zero && liveClass != fontClass))
            {
                result.Add("Game metadata absence inconclusive: incomplete enumeration or control mismatch.");
                return result;
            }

            foreach (var signature in new[]
            {
                ".ctor()", ".ctor(System.String)", ".ctor(System.String[], System.Int32)",
                "CreateDynamicFontFromOSFont(System.String, System.Int32)",
                "CreateDynamicFontFromOSFont(System.String[], System.Int32)",
                "GetOSInstalledFontNames()", "Internal_CreateFont(UnityEngine.Font, System.String)",
                "Internal_CreateFontFromPath(UnityEngine.Font, System.String)",
                "Internal_CreateDynamicFont(UnityEngine.Font, System.String[], System.Int32)",
            })
                if (!observed.Contains(signature))
                    result.Add("Game metadata: no exact signature match for " + signature);

            return result;
        }

        private static bool HasGameMethod(string image, string type, string methodName)
        {
            var klass = IL2CPP.GetIl2CppClass(image, "UnityEngine", type);
            if (klass == IntPtr.Zero) return false;
            IntPtr iterator = IntPtr.Zero;
            IntPtr method;
            while ((method = IL2CPP.il2cpp_class_get_methods(klass, ref iterator)) != IntPtr.Zero)
                if (IL2CPP.il2cpp_method_get_name_(method) == methodName && IL2CPP.il2cpp_method_get_param_count(method) == 0)
                    return true;
            return false;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string ResolveInternalCall(string name)
            => IL2CPP.il2cpp_resolve_icall("UnityEngine.Font::" + name) == IntPtr.Zero ? "unresolved" : "resolved";

        private static void Select(int index)
        {
            _selected = index;
            _previewed = _confirmed = _drawFailed = _defaultDrawFailed = false;
            _evidence = null;
        }

        private static bool Preview(Rect rect, Font font)
        {
            if (Event.current.type != EventType.Repaint || (font != null ? _drawFailed : _defaultDrawFailed)) return false;
            try
            {
                _previewStyle ??= new GUIStyle
                {
                    richText = false,
                    wordWrap = false,
                    normal = { textColor = Color.white },
                };
                _previewStyle.font = font;
                _previewStyle.fontSize = _size;
                _previewStyle.fontStyle = _bold ? FontStyle.Bold : FontStyle.Normal;
                GUI.Label(rect, Sample, _previewStyle);
                return true;
            }
            catch (Exception error)
            {
                if (font != null)
                {
                    Record("Preview failed: " + Identify(Candidates[_selected]) + " | " + Describe(error), log: false);
                    _drawFailed = true;
                    _confirmed = false;
                    if (_applied?.Font == font) HudFailed(error);
                }
                else
                    _defaultDrawFailed = true;
                Report(font != null ? "Candidate preview" : "IMGUI preview", error);
                return false;
            }
            finally
            {
                if (_previewStyle != null) _previewStyle.font = null;
            }
        }

        private static void Scan()
        {
            if (_applied != null && !_applied.Owned && _applied.Kind != "AssetBundle")
                ResetHud("Rescanning game fonts; HUD restored to IMGUI.");
            Candidates.RemoveAll(candidate => !candidate.Owned && candidate.Kind != "AssetBundle");
            Select(0);
            Acquire("GUI.skin.font", () => GUI.skin.font, false, "IMGUI Skin");
            Acquire("GUI.skin.label.font", () => GUI.skin.label.font, false, "IMGUI Skin");
            Acquire("Built-in Arial.ttf", () => GetBuiltin(), false, "Built-in Resource");
            try
            {
                var loaded = FindLoadedFonts();
                int limit = Candidates.Count + 64;
                foreach (var font in loaded)
                {
                    if (Candidates.Count >= limit) break;
                    Add(font, "Loaded Unity asset", false, "Game Asset");
                }
                _status = $"Loaded font scan returned {loaded.Length}; {Candidates.Count} unique live candidates retained.";
            }
            catch (Exception error) { Report("Loaded Unity font scan", error); }
            RecordStatus();
        }

        private static void ListOsFonts()
        {
            OsNames.Clear();
            _osSelected = 0;
            try
            {
                var names = GetOsNames();
                foreach (string name in names)
                    if (!string.IsNullOrWhiteSpace(name)) OsNames.Add(name);
                OsNames.Sort(StringComparer.CurrentCultureIgnoreCase);
                _status = $"Unity OS list: {OsNames.Count} name(s). Each candidate still needs a draw test.";
                Record(_status);
            }
            catch (Exception error) { Report("Unity OS enumeration", error); }
        }

        private static void Acquire(string source, Func<Font> get, bool owned, string kind, string requested = null)
        {
            // Keep at most one owned candidate per probe path. Applied probes
            // remain valid until replaced explicitly or reset.
            Font font = null;
            try
            {
                Record("Acquire: " + source + (requested == null ? "" : " [" + requested + "]"));
                font = get();
                if (font == null) { _status = source + ": no live font returned."; Record(_status); return; }
                var old = owned ? Candidates.Find(x => x.Source == source && x.Owned) : null;
                if (old != null && old.Font != font)
                {
                    if (_applied == old) ResetHud("Applied probe replaced. HUD restored to IMGUI.");
                    Candidates.Remove(old);
                    DestroyOwned(old);
                }
                Select(Add(font, source, owned, kind));
                Record("Acquired " + Identify(Candidates[_selected]));
                _status = source + ": acquired. Preview and inspect it separately.";
            }
            catch (Exception error)
            {
                if (owned && font != null && !Candidates.Exists(x => x.Font == font))
                    UnityEngine.Object.Destroy(font);
                Report(source, error);
            }
        }

        private static int Add(Font font, string source, bool owned, string kind, string bundlePath = null, string assetName = null)
        {
            if (font == null) return 0;
            int id = font.GetInstanceID();
            int index = Candidates.FindIndex(x => x.Id == id && x.Font != null
                && (bundlePath == null || (x.BundlePath == bundlePath && x.AssetName == assetName)));
            if (index >= 0) return index;
            Candidates.Add(new Candidate(font, source, font.name, id, owned, kind, Result(() => font.dynamic ? "Dynamic" : "Static"), bundlePath, assetName));
            return Candidates.Count - 1;
        }

        private static void Inspect(Font font)
        {
            string dynamicStatus = Result(() => font.dynamic.ToString());
            string coverage = Result(() =>
            {
                int hits = 0, count = 0;
                foreach (char c in "09Aaz日あカ汉體+-:.,%()[]")
                {
                    count++;
                    if (font.HasCharacter(c)) hits++;
                }
                return $"{hits}/{count} sample characters";
            });
            string requested = Result(() =>
            {
                if (!font.dynamic) return "skipped for static font";
                font.RequestCharactersInTexture(Sample.Replace("\n", " "), _size, _bold ? FontStyle.Bold : FontStyle.Normal);
                return "call returned";
            });
            string character = Result(() => CharacterInfoResult(font));
            string atlas = Result(() =>
            {
                var material = font.material;
                return material == null ? "no material" : material.mainTexture == null ? "material, no texture" : "live material and texture";
            });
            _evidence = $"Dynamic: {dynamicStatus}\nHasCharacter: {coverage}\nRequestCharactersInTexture: {requested}"
                + $"\nGetCharacterInfo (0): {character}\nAtlas: {atlas}";
            Record(Identify(Candidates[_selected]) + " | " + _evidence.Replace("\n", " | "));
        }

        private static string CharacterInfoResult(Font font)
        {
            // Some generated Store interop assemblies omit this member.
            // Only invoke an exposed overload with the documented signature.
            foreach (var method in typeof(Font).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.Name != "GetCharacterInfo" || method.ReturnType != typeof(bool)) continue;
                var args = method.GetParameters();
                if (args.Length != 4 || args[0].ParameterType != typeof(char) || !args[1].IsOut
                    || args[2].ParameterType != typeof(int) || args[3].ParameterType != typeof(FontStyle)) continue;
                return method.Invoke(font, new object[] { '0', null, _size, _bold ? FontStyle.Bold : FontStyle.Normal }).ToString();
            }
            return "not exposed by interop";
        }

        private static void SuspendHud(string status)
        {
            var previous = _applied;
            ResetHud(status);
            if (previous?.BundlePath != null) _pendingHud = (previous.BundlePath, previous.AssetName);
        }

        private static void ResetHud(string status)
        {
            var previous = _applied;
            _applied = null;
            _pendingHud = null;
            _status = status;
            if (previous == null) return;
            Record("Reset " + Identify(previous) + ". " + status);
            TryDetachHud(previous.BundlePath);
        }

        private static void TryDetachHud(string path)
        {
            try
            {
                DetachHudStyles?.Invoke();
                HudStylesRebound();
            }
            catch (Exception error)
            {
                if (path != null && Bundles.TryGetValue(path, out var slot))
                {
                    slot.AwaitingHudDetach = true;
                    _hudDetachPending = true;
                }
                Report("Detach HUD font styles", error);
            }
        }

        // A completed repaint assignment proves both native styles were rebound.
        internal static void HudStylesRebound()
        {
            if (!_hudDetachPending) return;
            _hudDetachPending = false;
            foreach (string path in new List<string>(Bundles.Keys))
            {
                var slot = Bundles[path];
                if (!slot.AwaitingHudDetach) continue;
                slot.AwaitingHudDetach = false;
                if (slot.DropAfterHudDetach) DropUnusedBundle(path);
                slot.DropAfterHudDetach = false;
            }
        }

        internal static void Release(bool keepSaved = false)
        {
            string retainedPath = keepSaved ? _savedPath : null;
            ResetHud(keepSaved && _saved != null ? "Saved HUD font restored." : "Standard IMGUI font restored.");
            if (!keepSaved)
            {
                ClearSavedFont();
                _savedPath = null;
                _savedAsset = null;
            }

            bool Retain(string path) => path != null && ((retainedPath != null && MatchesAsset(path, retainedPath))
                || (keepSaved && Bundles.TryGetValue(path, out var pending) && pending.AwaitingHudDetach));
            foreach (var candidate in Candidates)
                if (!Retain(candidate.BundlePath)) DestroyOwned(candidate);
            Candidates.RemoveAll(candidate => !Retain(candidate.BundlePath));
            _pendingPreview = null;
            _nextHealthCheck = 0;
            foreach (var path in new List<string>(Bundles.Keys))
            {
                var slot = Bundles[path];
                slot.DiagnosticOwner = false;
                if (Retain(path))
                {
                    if (!MatchesAsset(path, retainedPath)) slot.DropAfterHudDetach = true;
                    continue;
                }
                DisposeBundle(slot.Bundle);
                Bundles.Remove(path);
            }
            if (!keepSaved) _hudDetachPending = false;
            Select(0);
        }

        private static void DestroyOwned(Candidate candidate)
        {
            if (!candidate.Owned || candidate.Font == null) return;
            try { UnityEngine.Object.Destroy(candidate.Font); }
            catch (Exception error) { Report("Release probe font", error); }
        }

        private static string NameOf(Func<Font> get) => Result(() =>
        {
            var font = get();
            return font != null ? font.name : "inherited";
        });

        private static string Result(Func<string> action)
        {
            try { return action(); }
            catch (Exception error) { return Describe(error); }
        }

        private static void Report(string operation, Exception error)
        {
            _status = operation + ": " + Describe(error);
            Record(_status, log: false);
            if (Warnings.Add(operation)) Plugin.Logger.LogWarning("[Font Diagnostics] " + _status);
        }

        private static string Describe(Exception error)
        {
            if (error is TargetInvocationException { InnerException: { } inner }) error = inner;
            return error.GetType().Name + ": " + error.Message;
        }

        // Reflection keeps optional wrapper calls isolated across Store
        // interop builds. A wrapper may still fail at native invocation time.
        private static object InvokeStatic(Type owner, string method, Type[] types, params object[] args)
        {
            var api = owner.GetMethod(method, BindingFlags.Public | BindingFlags.Static, null, types, null);
            if (api == null) throw new MissingMethodException(owner.Name, method);
            return api.Invoke(null, args);
        }

        private static Il2CppStringArray GetOsNames()
            => (Il2CppStringArray)InvokeStatic(typeof(Font), "GetOSInstalledFontNames", Type.EmptyTypes);

        private static Font CreateSingle(string name, int size)
        {
            // The inspected regenerated single-family wrapper calls Font(string).
            // Original Unity 2019.4 source calls the array constructor instead.
            if (FindConstructor(typeof(string)) == null && FindConstructor(typeof(Il2CppStringArray), typeof(int)) == null)
                throw new MissingMethodException("Single-family probe skipped: neither Font(string) nor the array constructor is exposed.");
            return (Font)InvokeStatic(typeof(Font), "CreateDynamicFontFromOSFont",
                new[] { typeof(string), typeof(int) }, name, size);
        }

        private static Font CreateChain(int size)
        {
            RequireOsFontConstructor();
            return (Font)InvokeStatic(typeof(Font), "CreateDynamicFontFromOSFont",
                new[] { typeof(Il2CppStringArray), typeof(int) }, new Il2CppStringArray(Families), size);
        }

        private static ConstructorInfo FindConstructor(params Type[] signature)
            => typeof(Font).GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, signature, null);

        private static void RequireOsFontConstructor()
        {
            if (FindConstructor(typeof(Il2CppStringArray), typeof(int)) == null)
                throw new MissingMethodException("OS probe skipped: the Store interop lacks Font(Il2CppStringArray, int).");
        }

        private static Font CreateByName(string name)
        {
            var constructor = FindConstructor(typeof(string));
            if (constructor == null || !constructor.IsPublic)
                throw new MissingMethodException("The public Font(string) constructor is unavailable.");
            return (Font)constructor.Invoke(new object[] { name });
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static Font GetBuiltin() => Resources.GetBuiltinResource<Font>("Arial.ttf");

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static Il2CppArrayBase<Font> FindLoadedFonts() => Resources.FindObjectsOfTypeAll<Font>();
    }
}
#endif
