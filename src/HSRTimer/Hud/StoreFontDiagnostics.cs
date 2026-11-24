#if STORE_IL2CPP
using System;
using System.Collections.Generic;
using System.Reflection;
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
        private sealed record Candidate(Font Font, string Source, string Name, int Id, bool Owned);
        private static readonly List<Candidate> Candidates = new();
        private static readonly List<string> OsNames = new();
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
                _open = !_open;
            if (!_open) return;

            GUILayout.Label("Session only. The settings panel keeps its own font.", label);
            GUILayout.Label("IMGUI skin: " + NameOf(() => GUI.skin.font)
                + " | label override: " + NameOf(() => GUI.skin.label.font)
                + " | Timer HUD: " + (_applied == null ? "IMGUI skin" : _applied.Name), label);

            if (GUILayout.Button("Scan game fonts", button)) Scan();
            if (GUILayout.Button("List Unity OS fonts", button)) ListOsFonts();
            IReadOnlyList<string> names = OsNames.Count > 0 ? OsNames : Families;
            GUILayout.Label($"OS font {_osSelected + 1}/{names.Count}: {names[_osSelected]}"
                + (OsNames.Count == 0 ? " (upstream choice)" : ""), label);
            if (GUILayout.Button("Previous OS font", button)) _osSelected = (_osSelected + names.Count - 1) % names.Count;
            if (GUILayout.Button("Next OS font", button)) _osSelected = (_osSelected + 1) % names.Count;
            if (GUILayout.Button("Probe selected OS font", button))
                Acquire("Unity single family", () => CreateSingle(names[_osSelected], _size), true);
            if (GUILayout.Button("Probe upstream fallback order", button))
                Acquire("Unity upstream font chain", () => CreateChain(_size), true);
            if (GUILayout.Button("Restore IMGUI / release probe fonts", button))
            {
                Release();
                _status = "Standard IMGUI font restored.";
            }

            GUILayout.Label(_status, label);
            if (Candidates.Count == 0) return;
            if (GUILayout.Button("Previous candidate", button)) Select((_selected + Candidates.Count - 1) % Candidates.Count);
            if (GUILayout.Button("Next candidate", button)) Select((_selected + 1) % Candidates.Count);
            var candidate = Candidates[_selected];
            GUILayout.Label($"Candidate {_selected + 1}/{Candidates.Count}: {candidate.Name} #{candidate.Id} ({candidate.Source})", label);
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
            if (Preview(GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true)), candidate.Font)) _previewed = true;
            GUILayout.Label(_drawFailed ? "Draw call failed." : _confirmed ? "Visually checked by user."
                : _previewed ? "Draw call returned. Check for blank or missing glyphs." : "Waiting for repaint.", label);
            // Keep the control tree stable across IMGUI Layout and Repaint.
            if (GUILayout.Button("I checked the candidate visually", button) && _previewed && !_drawFailed)
                _confirmed = true;
            if (GUILayout.Button("Apply to Timer HUD for this session", button) && _confirmed && !_drawFailed)
            {
                _applied = candidate;
                _appliedSize = _size;
                _status = "Applied to the timer rows and warning banner. Check during play and after Retry.";
            }
        }

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
            Release();
            Acquire("GUI.skin.font", () => GUI.skin.font, false);
            Acquire("GUI.skin.label.font", () => GUI.skin.label.font, false);
            Acquire("Built-in Arial.ttf", () => GetBuiltin(), false);
            try
            {
                var loaded = FindLoadedFonts();
                foreach (var font in loaded)
                {
                    if (Candidates.Count >= 64) break;
                    Add(font, "Loaded Unity asset", false);
                }
                _status = $"Loaded font scan returned {loaded.Length}; {Candidates.Count} unique live candidates retained.";
            }
            catch (Exception error) { Report("Loaded Unity font scan", error); }
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
            }
            catch (Exception error) { Report("Unity OS enumeration", error); }
        }

        private static void Acquire(string source, Func<Font> get, bool owned)
        {
            // Keep at most one owned candidate per probe path. Applied probes
            // remain valid until replaced explicitly or reset.
            Font font = null;
            try
            {
                font = get();
                if (font == null) { _status = source + ": no live font returned."; return; }
                var old = owned ? Candidates.Find(x => x.Source == source && x.Owned) : null;
                if (old != null && old.Font != font)
                {
                    if (_applied == old) ResetHud("Applied probe replaced. HUD restored to IMGUI.");
                    Candidates.Remove(old);
                    DestroyOwned(old);
                }
                Select(Add(font, source, owned));
                _status = source + ": acquired. Preview and inspect it separately.";
            }
            catch (Exception error)
            {
                if (owned && font != null && !Candidates.Exists(x => x.Font == font))
                    UnityEngine.Object.Destroy(font);
                Report(source, error);
            }
        }

        private static int Add(Font font, string source, bool owned)
        {
            if (font == null) return 0;
            int id = font.GetInstanceID();
            int index = Candidates.FindIndex(x => x.Id == id && x.Font != null);
            if (index >= 0) return index;
            Candidates.Add(new Candidate(font, source, font.name, id, owned));
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
            Plugin.Logger.LogInfo("[Font Diagnostics] " + _evidence.Replace("\n", " | "));
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
            _applied = null;
            _status = status;
        }

        internal static void Release()
        {
            ResetHud("Standard IMGUI font restored.");
            foreach (var candidate in Candidates) DestroyOwned(candidate);
            Candidates.Clear();
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
            RequireOsFontConstructor();
            return (Font)InvokeStatic(typeof(Font), "CreateDynamicFontFromOSFont",
                new[] { typeof(string), typeof(int) }, name, size);
        }

        private static Font CreateChain(int size)
        {
            RequireOsFontConstructor();
            return (Font)InvokeStatic(typeof(Font), "CreateDynamicFontFromOSFont",
                new[] { typeof(Il2CppStringArray), typeof(int) }, new Il2CppStringArray(Families), size);
        }

        private static void RequireOsFontConstructor()
        {
            if (typeof(Font).GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(Il2CppStringArray), typeof(int) }, null) == null)
                throw new MissingMethodException("OS probe skipped: the Store interop lacks Font(Il2CppStringArray, int).");
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static Font GetBuiltin() => Resources.GetBuiltinResource<Font>("Arial.ttf");

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static Il2CppArrayBase<Font> FindLoadedFonts() => Resources.FindObjectsOfTypeAll<Font>();
    }
}
#endif
