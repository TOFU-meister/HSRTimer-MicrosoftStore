#if STORE_IL2CPP
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace HSRTimer
{
    internal sealed class StoreHudFontPanel
    {
        private IReadOnlyList<string> _files;
        private IReadOnlyList<StoreFontDiagnostics.FontChoice> _choices;
        private string _bundle, _asset, _ownedPath, _message;
        private StoreHudFontStyle _weight;
        private GUIStyle _preview;
        private bool _showFiles;

        internal void Close()
        {
            DropPreview();
            _files = null;
            _showFiles = false;
            _message = null;
        }

        private void DropPreview()
        {
            if (_preview != null) _preview.font = null;
            _choices = null;
            StoreFontDiagnostics.DropUnusedBundle(_ownedPath);
            _ownedPath = null;
        }

        private void Select(string bundle, string asset)
        {
            DropPreview();
            _bundle = bundle;
            _asset = asset;
            _message = null;
            if (bundle == null) return;
            try
            {
                _choices = StoreFontDiagnostics.ReadFontChoices(PersistenceService.PluginDir, bundle, out _ownedPath);
                if (_asset == null) _asset = _choices[0].AssetName;
                if (SelectedChoice() == null) _message = "HUD_FONT_UNAVAILABLE";
            }
            catch (Exception error) { _message = StoreFontDiagnostics.FontIssue(error); }
        }

        private StoreFontDiagnostics.FontChoice SelectedChoice()
        {
            if (_choices == null) return null;
            foreach (var choice in _choices)
                if (string.Equals(choice.AssetName, _asset, StringComparison.OrdinalIgnoreCase)) return choice;
            return null;
        }

        private void Scan()
        {
            try { _files = StoreFontDiagnostics.ListFontBundles(PersistenceService.PluginDir); }
            catch (Exception error)
            {
                _files = Array.Empty<string>();
                _message = StoreFontDiagnostics.FontIssue(error);
            }
        }

        internal void Draw(SettingsModel settings, int size, LocalizationService loc, GUIStyle button, GUIStyle label)
        {
            if (_files == null)
            {
                _weight = settings.HudFont.Style;
                Select(settings.HudFont.BundleFileName, settings.HudFont.AssetName);
                Scan();
            }

            string defaultName = loc.Get("HUD_FONT_DEFAULT");
            GUILayout.Label(loc.Get("HUD_FONT_SAVED", settings.HudFont.BundleFileName ?? defaultName), label);
            GUILayout.Label(loc.Get("HUD_FONT_CURRENT", StoreFontDiagnostics.CurrentFontName ?? defaultName), label);
            if (settings.HudFont.IsExternal && StoreFontDiagnostics.SavedFontIssue is { } issue)
                GUILayout.Label(loc.Get(issue), label);

            if (GUILayout.Button(loc.Get("HUD_FONT_SELECT", _bundle ?? defaultName), button))
                _showFiles = !_showFiles;
            if (_bundle != null)
            {
                var selected = SelectedChoice();
                if (_choices?.Count > 1 && GUILayout.Button(loc.Get("HUD_FONT_ASSET", selected?.Name ?? _asset), button))
                {
                    int index = 0;
                    while (index < _choices.Count && _choices[index] != selected) index++;
                    _asset = _choices[index == _choices.Count ? 0 : (index + 1) % _choices.Count].AssetName;
                    _message = null;
                }
                else if (_choices?.Count == 1 && selected == null && GUILayout.Button(loc.Get("HUD_FONT_ASSET", _choices[0].Name), button))
                {
                    _asset = _choices[0].AssetName;
                    _message = null;
                }
                if (GUILayout.Button(loc.Get(_weight == StoreHudFontStyle.Bold ? "HUD_FONT_BOLD" : "HUD_FONT_NORMAL"), button))
                    _weight = _weight == StoreHudFontStyle.Bold ? StoreHudFontStyle.Normal : StoreHudFontStyle.Bold;
            }

            DrawPreview(size, loc);
            if (_message != null) GUILayout.Label(loc.Get(_message), label);
            if (GUILayout.Button(loc.Get("HUD_FONT_APPLY"), button))
            {
                var preference = StoreHudFontPreference.Imgui;
                if (_bundle == null || StoreHudFontPreference.TryExternal(_bundle, _asset, _weight, out preference))
                    _message = StoreFontDiagnostics.ApplyAndSaveFont(settings, preference, PersistenceService.PluginDir) ?? "HUD_FONT_SAVED_OK";
                else
                    _message = "HUD_FONT_UNAVAILABLE";
            }
            if (GUILayout.Button(loc.Get("HUD_FONT_CANCEL"), button))
            {
                _weight = settings.HudFont.Style;
                Select(settings.HudFont.BundleFileName, settings.HudFont.AssetName);
                _showFiles = false;
            }
            if (GUILayout.Button(loc.Get("HUD_FONT_DEFAULT"), button))
            {
                Select(null, null);
                _showFiles = false;
            }
            if (GUILayout.Button(loc.Get("HUD_FONT_RELOAD"), button))
            {
                Select(_bundle, _asset);
                Scan();
                _showFiles = true;
            }
            if (GUILayout.Button(loc.Get("HUD_FONT_FOLDER"), button))
            {
                try
                {
                    string path = StoreFontDiagnostics.FontsDirectory(PersistenceService.PluginDir);
                    Directory.CreateDirectory(path);
                    using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "explore" });
                }
                catch (Exception) { _message = "HUD_FONT_FOLDER_FAILED"; }
            }

            if (!_showFiles) return;
            if (_files.Count == 0) GUILayout.Label(loc.Get("HUD_FONT_EMPTY"), label);
            foreach (string file in _files)
            {
                if (!GUILayout.Button(file, button)) continue;
                if (_bundle == null) _weight = StoreHudFontStyle.Normal;
                Select(file, null);
                _showFiles = false;
                break;
            }
        }

        private void DrawPreview(int size, LocalizationService loc)
        {
            int previewSize = Mathf.Clamp(size, 8, 26);
            var rect = GUILayoutUtility.GetRect(1f, (previewSize + 5) * 3, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint) return;
            try
            {
                var font = SelectedChoice()?.Font;
                if (_bundle != null && font == null) return;
                _preview ??= new GUIStyle { normal = { textColor = Color.white }, richText = false };
                _preview.font = font;
                _preview.fontSize = previewSize;
                _preview.fontStyle = _bundle == null || _weight == StoreHudFontStyle.Bold ? FontStyle.Bold : FontStyle.Normal;
                GUI.Label(rect, loc.Get("HUD_FONT_SAMPLE"), _preview);
            }
            catch (Exception) { _message = "HUD_FONT_UNAVAILABLE"; }
            finally { if (_preview != null) _preview.font = null; }
        }
    }
}
#endif
