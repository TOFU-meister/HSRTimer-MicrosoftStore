using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public class Il2CppArrayBase<T> : List<T>
    {
        public Il2CppArrayBase(IEnumerable<T> values) : base(values) {}
        public int Length => Count;
    }

    public class Il2CppStringArray : Il2CppArrayBase<string>
    {
        public Il2CppStringArray(string[] values) : base(values) {}
    }
}

namespace UnityEngine
{
    public static class Object
    {
        public static void Destroy(Font font) => font.Destroyed = true;
    }

    public sealed class Font
    {
        private static int _next;
        private readonly int _id = ++_next;
        public static Font LastCreated;
        public static bool FailOsList = true;
        public bool Destroyed;
        public bool dynamic => true;
        public string name { get; set; } = "Probe";
        public Material material { get; } = new();
        public Font() {}
        private Font(Il2CppStringArray names, int size) => name = names[0];
        public static Font CreateDynamicFontFromOSFont(Il2CppStringArray names, int size)
            => LastCreated = new Font(names, size);
        // Single-family overload deliberately missing in these interop stubs.
        public static Il2CppStringArray GetOSInstalledFontNames() => FailOsList
            ? throw new MissingMethodException("OS enumeration")
            : new Il2CppStringArray(new[] { "Segoe UI", "Arial" });
        public int GetInstanceID() => _id;
        public bool HasCharacter(char c) => true;
        public void RequestCharactersInTexture(string text, int size, FontStyle style) {}
        private static bool Missing(Font f) => f is null || f.Destroyed;
        public static bool operator ==(Font a, Font b) => Missing(a) && Missing(b) || ReferenceEquals(a, b);
        public static bool operator !=(Font a, Font b) => !(a == b);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => _id;
    }

    public sealed class Material { public object mainTexture = new(); }
    public struct Rect {}
    public enum FontStyle { Normal, Bold }
    public enum EventType { Repaint }
    public sealed class Event { public static Event current = new(); public EventType type; }

    public sealed class GUIStyleState { public Color textColor; }

    public sealed class GUIStyle
    {
        public readonly GUIStyleState normal = new();
        public Font font;
        public int fontSize;
        public FontStyle fontStyle;
        public bool richText, wordWrap;
    }

    public static class GUI
    {
        public static readonly Skin skin = new();
        public static bool FailCandidateDraw;
        public static void Label(Rect rect, string text, GUIStyle style)
        {
            if (FailCandidateDraw && style.font != null)
                throw new InvalidOperationException("preview draw failed");
        }
        public sealed class Skin
        {
            public readonly Font font = new() { name = "IMGUI" };
            public readonly GUIStyle label = new();
        }
    }

    public static class GUILayout
    {
        public static string Click;
        public static readonly List<string> Labels = new();
        public static bool Button(string label, GUIStyle style) => label == Click;
        public static bool Toggle(bool enabled, string label) => enabled;
        public static void Label(string label, GUIStyle style) => Labels.Add(label);
        public static object ExpandWidth(bool expand) => null;
    }

    public static class GUILayoutUtility
    {
        public static Rect GetRect(float width, float height, object option) => new();
    }

    public static partial class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : class => null;
        public static Il2CppArrayBase<T> FindObjectsOfTypeAll<T>() => new(Array.Empty<T>());
    }
}
