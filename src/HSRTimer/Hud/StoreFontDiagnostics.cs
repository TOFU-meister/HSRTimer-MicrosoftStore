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
        private const string Sample = "0123456789  00:12:34.567\n-00:01.234  +00:02.345\nPersonal Best / Human: Fall Flat\n"
            + "ABCDEFGHIJKLMNOPQRSTUVWXYZ\nabcdefghijklmnopqrstuvwxyz\n日本語・ひらがな・カタカナ\n漢字・简体中文・繁體中文\n+-:.,%()[]";
        private static readonly string[] Families =
        {
            "PingFang SC", "Microsoft YaHei", "Noto Sans CJK SC", "Noto Sans CJK",
            "Heiti SC", "Arial Unicode MS", "Arial",
        };
        private sealed record Candidate(Font Font, string Source, string Name, int Id, bool Owned, string Kind, string Style);
        private static readonly List<Candidate> Candidates = new();
        private static readonly List<string> OsNames = new();
        private static readonly Dictionary<string, StoreFontBundle> Bundles = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Queue<string> History = new();
        private static string _search = "";
        private static string _bundleResult;
        private static Vector2 _osScroll, _candidateScroll;
        private static readonly HashSet<string> Warnings = new();
        private static int _selected, _osSelected;
        private static int _size = 18;
        private static bool _open, _bold, _previewed, _confirmed, _drawFailed, _defaultDrawFailed;
        private static Candidate _applied;
        private static GUIStyle _previewStyle;
        private static int _appliedSize;
        private static string _status = "Scan or probe to begin.";
        private static string _evidence;

        internal static Font ActiveFont()
        {
            if (_applied == null) return null;
            if (_applied.Font != null) return _applied.Font;
            ResetHud("The applied font was unloaded. HUD restored to IMGUI.");
            return null;
        }

        internal static int ActiveSize => _appliedSize;
        internal static bool IsApplied => _applied != null;
        internal static void HudFailed(Exception error)
        {
            if (Candidates.Count > 0 && Candidates[_selected] == _applied)
            {
                _confirmed = false;
                _previewed = false;
            }
            ResetHud("Timer HUD fell back. " + Describe(error));
            if (Warnings.Add("Timer HUD")) Plugin.Logger.LogWarning("[Font Diagnostics] " + _status);
        }

        internal static void Draw(GUIStyle button, GUIStyle label)
        {
            if (GUILayout.Button(_open ? "Hide Font Diagnostics" : "Font Diagnostics", button))
            {
                _open = !_open;
                if (!_open) PanelTextInput.Cancel();
            }
            if (!_open) return;

            GUILayout.Label("Session only. The settings panel keeps its own font.", label);
            GUILayout.Label("IMGUI skin: " + NameOf(() => GUI.skin.font)
                + " | label override: " + NameOf(() => GUI.skin.label.font)
                + " | Timer HUD: " + (_applied == null ? "IMGUI skin" : _applied.Name), label);

            if (GUILayout.Button("Scan game fonts", button)) Scan();
            if (GUILayout.Button("Inspect interop and internal-call availability", button)) ProbeInterop();
            if (GUILayout.Button("Load local font bundles (file)", button)) LoadBundles(StoreFontBundle.Route.File);
            if (GUILayout.Button("Compare file (CRC 0, offset 0)", button)) LoadBundles(StoreFontBundle.Route.FileExplicit);
            if (GUILayout.Button("Compare memory", button)) LoadBundles(StoreFontBundle.Route.Memory);
            if (GUILayout.Button("Compare memory (CRC 0)", button)) LoadBundles(StoreFontBundle.Route.MemoryExplicit);
            if (_bundleResult != null) GUILayout.Label(_bundleResult, label);
            if (GUILayout.Button("Record current status", button)) RecordStatus();
            if (GUILayout.Button("Export diagnostic report", button)) ExportReport();
            if (GUILayout.Button("Restore IMGUI / release probe fonts", button)) Release();
            DrawOsFonts(button, label);
            GUILayout.Label(_status, label);

            if (Candidates.Count == 0) return;
            _candidateScroll = GUILayout.BeginScrollView(_candidateScroll, GUILayout.Height(112f));
            try
            {
                for (int i = 0; i < Candidates.Count; i++)
                    if (GUILayout.Button((i == _selected ? "▶ " : "") + Identify(Candidates[i]), button))
                    {
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

            float height = (_size + 6) * 8;
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
                _applied = candidate;
                _appliedSize = _size;
                Record("HUD applied: " + Identify(candidate) + $" at {_size}px");
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
            if (History.Count >= 256) History.Dequeue();
            History.Enqueue(DateTime.UtcNow.ToString("O") + " " + message);
            if (log) Plugin.Logger.LogInfo("[Font Diagnostics] " + message);
        }

        private static void RecordStatus()
        {
            Record("Status at " + Result(() => Application.loadedLevelName) + ": IMGUI=" + NameOf(() => GUI.skin.font)
                + "; OS font count=" + OsNames.Count + "; HUD=" + (_applied == null ? "IMGUI" : Identify(_applied)));
            foreach (var candidate in Candidates)
                Record(Identify(candidate) + (candidate.Font != null ? " / live" : " / unloaded"));
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
                    if (Bundles.Count >= StoreFontBundle.MaxBundles || attempts >= StoreFontBundle.MaxBundles) break;
                    if (Bundles.ContainsKey(path))
                    {
                        Note("Skipped " + Path.GetFileName(path) + ": already open. Restore IMGUI and release before retrying.");
                        continue;
                    }
                    attempts++;
                    try
                    {
                        StoreDataPolicy.EnsureSafePath(PersistenceService.PluginDir, path);
                        var bundle = LoadWithUnityMessages(path, route, Note);
                        int before = Candidates.Count;
                        try
                        {
                            foreach (var font in bundle.Fonts)
                                Select(Add(font, Path.GetFileName(path), false, "AssetBundle"));
                            Bundles.Add(path, bundle);
                        }
                        catch
                        {
                            Candidates.RemoveRange(before, Candidates.Count - before);
                            Select(0);
                            bundle.Dispose();
                            throw;
                        }
                        added++;
                        Note($"Loaded {Path.GetFileName(path)} ({bundle.Fonts.Count} Font; {bundle.TextureCount} Texture2D).");
                    }
                    catch (Exception error) { Note("Load bundle " + Path.GetFileName(path) + ": " + StoreFontBundle.Describe(error)); }
                }
                Note($"Loaded {added} new bundle(s); {Bundles.Count}/{StoreFontBundle.MaxBundles} open. Folder: {folder}");
            }
            catch (Exception error) { Note("Load local font bundles: " + StoreFontBundle.Describe(error)); }
            _bundleResult = string.Join("\n", details);
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

        private static int Add(Font font, string source, bool owned, string kind)
        {
            if (font == null) return 0;
            int id = font.GetInstanceID();
            int index = Candidates.FindIndex(x => x.Id == id && x.Font != null);
            if (index >= 0) return index;
            Candidates.Add(new Candidate(font, source, font.name, id, owned, kind, Result(() => font.dynamic ? "Dynamic" : "Static")));
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

        private static void ResetHud(string status)
        {
            if (_applied != null) Record("Reset " + Identify(_applied) + ". " + status);
            _applied = null;
            _status = status;
        }

        internal static void Release()
        {
            ResetHud("Standard IMGUI font restored.");
            foreach (var candidate in Candidates) DestroyOwned(candidate);
            Candidates.Clear();
            foreach (var bundle in Bundles.Values)
            {
                try { bundle.Dispose(); }
                catch (Exception error) { Report("Unload font bundle", error); }
            }
            Bundles.Clear();
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
