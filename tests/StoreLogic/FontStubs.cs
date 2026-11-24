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
    public class Object : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        public static void Destroy(Font font) => font.Destroyed = true;
    }

    public sealed class Font : Object
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
    public struct Vector2 { public static Vector2 zero => default; }
    public static class Application { public static string loadedLevelName => "Test"; }
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
        public static object Height(float value) => null;
        public static Vector2 BeginScrollView(Vector2 position, object option) => position;
        public static void EndScrollView() {}
    }

    public static class GUILayoutUtility
    {
        public static Rect GetRect(float width, float height, object option) => new();
    }

    public static partial class Resources
    {
        public static Font[] LoadedFonts = Array.Empty<Font>();
        public static T GetBuiltinResource<T>(string path) where T : class => null;
        public static Il2CppArrayBase<T> FindObjectsOfTypeAll<T>() => new((T[])(object)LoadedFonts);
    }
}

namespace Il2CppSystem { public class Type {} }
namespace Il2CppInterop.Runtime
{
    public static class Il2CppType { public static Il2CppSystem.Type Of<T>() => new(); }
    public static class IL2CPP
    {
        public static bool HasFontClass = true;
        public static IntPtr il2cpp_resolve_icall(string name) => IntPtr.Zero;
        public static IntPtr GetIl2CppClass(string image, string ns, string name)
            => HasFontClass ? new IntPtr(1) : IntPtr.Zero;
        public static IntPtr il2cpp_class_get_methods(IntPtr type, ref IntPtr iterator)
        {
            if (iterator != IntPtr.Zero) return IntPtr.Zero;
            return iterator = new IntPtr(1);
        }
        public static string il2cpp_method_get_name_(IntPtr method) => ".ctor";
        public static uint il2cpp_method_get_param_count(IntPtr method) => 1;
        public static IntPtr il2cpp_method_get_param(IntPtr method, uint index) => new(1);
        public static string il2cpp_type_get_name_(IntPtr parameter) => "System.String";
        public static uint il2cpp_method_get_token(IntPtr method) => 0x06000001;
    }
}
namespace Il2CppInterop.Runtime.InteropTypes
{
    public class Il2CppObjectBase { public T TryCast<T>() where T : class => this as T; }
}
namespace HSRTimer
{
    internal static class PanelTextInput
    {
        internal static string Query = "";
        internal static void Cancel() {}
        internal static string Draw(string key, string value, UnityEngine.GUIStyle style) => Query;
    }
}
public sealed class TestFontBundle
{
    public static TestFontBundle Next;
    public static int Opened;
    public string[] Names = { "assets/fonts/test.ttf" };
    public UnityEngine.Font Font = new();
    public int Unloads;
    public static TestFontBundle LoadFromFile(string path) { Opened++; return Next; }
    public string[] GetAllAssetNames() => Names;
    public UnityEngine.Object LoadAsset(string name, Il2CppSystem.Type type) => Font;
    public void Unload(bool destroyAssets) { Unloads++; if (destroyAssets && Font != null) UnityEngine.Object.Destroy(Font); }
}
