#if STORE_IL2CPP
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace HSRTimer
{
    internal sealed class StoreFontBundle : IDisposable
    {
        internal const int MaxBundles = 4;
        internal const int MaxAssets = 32;
        internal const long MaxFileBytes = 32 * 1024 * 1024;
        private const string Module = "UnityEngine.AssetBundleModule";
        private object _bundle;
        private readonly MethodInfo _unload;
        internal enum Route { File, FileExplicit, Memory, MemoryExplicit }
        internal int TextureCount { get; private set; }
        internal List<Font> Fonts { get; } = new();
        internal IReadOnlyList<string> FontNames => _fontNames;
        private readonly List<string> _fontNames = new();
        private readonly List<FontResources> _resources = new();

        private sealed class FontResources
        {
            private readonly Font _font;
            private Material _material;
            private Texture _atlas;
            private bool _hadMaterial, _hadAtlas;

            internal FontResources(Font font) => _font = font;

            internal string Check()
            {
                if (_font == null) return "Font invalid";
                _material = _font.material;
                if (_material == null && _hadMaterial) return "Material invalid";
                _hadMaterial |= _material != null;
                _atlas = _material == null ? null : _material.mainTexture;
                if (_atlas == null && _hadAtlas) return "atlas Texture invalid";
                _hadAtlas |= _atlas != null;
                return null;
            }

            internal string Status => $"Font={(_font != null ? "live, flags=" + _font.hideFlags : "invalid")}, "
                + $"Material={ResourceStatus(_material, _hadMaterial)}, Texture={ResourceStatus(_atlas, _hadAtlas)}";

            private static string ResourceStatus(UnityEngine.Object value, bool seen)
                => value != null ? "live, flags=" + value.hideFlags : seen ? "invalid" : "not allocated";
        }

        private bool IsAlive => _bundle != null && (_bundle is not UnityEngine.Object unity || unity != null);

        internal string CheckHealth()
        {
            if (!IsAlive) return "Bundle invalid";
            foreach (var resource in _resources)
            {
                var reason = resource.Check();
                if (reason != null) return reason;
            }
            return null;
        }

        internal string ResourceStatus(int index) => $"Bundle={(IsAlive ? "live" : "invalid")}, " + _resources[index].Status;

        private StoreFontBundle(object bundle, MethodInfo unload)
        {
            _bundle = bundle;
            _unload = unload;
        }

        internal static Type FindApi()
            => Type.GetType("UnityEngine.AssetBundle, " + Module, throwOnError: false);

        internal static string ApiStatus()
        {
            var type = FindApi();
            if (type == null) return Module + " / UnityEngine.AssetBundle unavailable";
            return string.Join("; ", new[]
            {
                Status(type, "LoadFromFile", typeof(string)),
                Status(type, "LoadFromFile", typeof(string), typeof(uint), typeof(ulong)),
                Status(type, "LoadFromMemory", typeof(Il2CppStructArray<byte>)),
                Status(type, "LoadFromMemory", typeof(Il2CppStructArray<byte>), typeof(uint)),
                Status(type, "GetAllAssetNames"),
                Status(type, "LoadAsset", typeof(string), typeof(Il2CppSystem.Type)),
                Status(type, "Unload", typeof(bool)),
            });
        }

        private static string Status(Type type, string name, params Type[] signature)
            => $"{name}({string.Join(", ", Array.ConvertAll(signature, t => t.Name))})="
                + (type.GetMethod(name, signature) == null ? "missing" : "present");

        internal static string RouteName(Route route) => route switch
        {
            Route.File => "LoadFromFile(string)",
            Route.FileExplicit => "LoadFromFile(string, uint, ulong)",
            Route.Memory => "LoadFromMemory(byte[])",
            Route.MemoryExplicit => "LoadFromMemory(byte[], uint)",
            _ => throw new ArgumentOutOfRangeException(nameof(route)),
        };

        internal static StoreFontBundle Load(string path, Route route, Action<string> report)
            => Load(path, FindApi(), () => Il2CppType.Of<Font>(), route, report);

        internal static StoreFontBundle Load(string path, Type type, Func<Il2CppSystem.Type> fontType,
            Route route = Route.File, Action<string> report = null)
        {
            report ??= _ => { };
            byte[] bytes = null;
            using (var stream = File.OpenRead(path))
            {
                report($"Detected {Path.GetFileName(path)} ({stream.Length} bytes); API={RouteName(route)}");
                if (stream.Length < 8 || stream.Length > MaxFileBytes)
                    throw new InvalidDataException("File validation: expected a UnityFS file up to 32 MiB.");
                foreach (char expected in "UnityFS\0")
                    if (stream.ReadByte() != expected) throw new InvalidDataException("File validation: invalid UnityFS header.");
                if (route == Route.Memory || route == Route.MemoryExplicit)
                {
                    stream.Position = 0;
                    int expectedLength = (int)stream.Length;
                    using var reader = new BinaryReader(stream);
                    bytes = reader.ReadBytes(expectedLength);
                    if (bytes.Length != expectedLength)
                        throw new EndOfStreamException("File read: bundle size changed while reading.");
                }
            }

            if (type == null) throw new NotSupportedException(Module + " is unavailable in this Store interop.");
            Type byteArray = typeof(Il2CppStructArray<byte>);
            var load = route switch
            {
                Route.File => Require(type, "LoadFromFile", typeof(string)),
                Route.FileExplicit => Require(type, "LoadFromFile", typeof(string), typeof(uint), typeof(ulong)),
                Route.Memory => Require(type, "LoadFromMemory", byteArray),
                Route.MemoryExplicit => Require(type, "LoadFromMemory", byteArray, typeof(uint)),
                _ => throw new ArgumentOutOfRangeException(nameof(route)),
            };
            var names = Require(type, "GetAllAssetNames");
            var asset = Require(type, "LoadAsset", typeof(string), typeof(Il2CppSystem.Type));
            var unload = Require(type, "Unload", typeof(bool));
            object[] arguments = route switch
            {
                Route.File => new object[] { path },
                Route.FileExplicit => new object[] { path, 0u, 0ul },
                Route.Memory => new object[] { new Il2CppStructArray<byte>(bytes) },
                _ => new object[] { new Il2CppStructArray<byte>(bytes), 0u },
            };
            var result = Invoke(load, null, RouteName(route), arguments);
            if (result is null)
                throw new InvalidDataException("Open stage: CLR null returned. Unity did not return a bundle object.");
            if (result is Il2CppObjectBase wrapper)
            {
                bool hasPointer;
                try { hasPointer = wrapper.Pointer != IntPtr.Zero; }
                catch (Exception error) { throw new InvalidDataException("Open stage: IL2CPP object lookup failed.", error); }
                report($"Open stage: IL2CPP wrapper {result.GetType().FullName}, object pointer present={hasPointer}.");
                if (!hasPointer) throw new InvalidDataException("Open stage: wrapper has no IL2CPP object.");
            }
            if (result is UnityEngine.Object unityBundle && unityBundle == null)
                throw new InvalidDataException("Open stage: CLR wrapper present; Unity overloaded null reports an invalid native object.");
            report(result is UnityEngine.Object ? "Open stage: Unity native object is valid." : "Open stage: object returned.");

            var loaded = new StoreFontBundle(result, unload);
            try
            {
                if (Invoke(names, result, "GetAllAssetNames") is not IEnumerable assets)
                    throw new InvalidDataException("Enumeration stage: no usable asset list.");
                var entries = new List<string>();
                foreach (string name in assets)
                {
                    if (entries.Count >= MaxAssets) throw new InvalidDataException("Enumeration stage: more than 32 named assets.");
                    entries.Add(name);
                }
                var requestedFont = fontType();
                foreach (string name in entries)
                {
                    report("Asset: " + name);
                    var value = Invoke(asset, result, "LoadAsset(Font) " + name, name, requestedFont);
                    var font = Cast<Font>(value, name);
                    if (font != null)
                    {
                        loaded.Fonts.Add(font);
                        loaded._fontNames.Add(name);
                        var resources = new FontResources(font);
                        loaded._resources.Add(resources);
                        try { font.hideFlags |= HideFlags.DontUnloadUnusedAsset; }
                        catch (Exception error) { report("Owned Font protection unavailable: " + Describe(error)); }
                        resources.Check();
                        report("Asset result: valid Font " + font.name + " #" + font.GetInstanceID() + "; " + resources.Status);
                        continue;
                    }
                    report(value is null ? "Asset result: Font lookup returned CLR null."
                        : "Asset result: returned object did not cast to a valid Font.");
                    value = Invoke(asset, result, "LoadAsset(Texture2D) " + name, name, Il2CppType.Of<Texture2D>());
                    var texture = Cast<Texture2D>(value, name);
                    if (texture != null)
                    {
                        loaded.TextureCount++;
                        report("Asset result: valid Texture2D " + texture.name);
                    }
                    else
                        report(value is null ? "Asset result: Texture2D lookup returned CLR null."
                            : "Asset result: returned object did not cast to a valid Texture2D.");
                }

                if (loaded.Fonts.Count == 0 && loaded.TextureCount == 0)
                    throw new InvalidDataException("Asset stage: no readable Font or Texture2D.");
                return loaded;
            }
            catch
            {
                try { loaded.Dispose(); }
                catch (Exception error) { report("Cleanup failed: " + Describe(error)); }
                throw;
            }
        }

        private static T Cast<T>(object value, string name) where T : Il2CppObjectBase
        {
            try { return value as T ?? (value as Il2CppObjectBase)?.TryCast<T>(); }
            catch (Exception error)
            {
                throw new InvalidDataException($"Asset stage: {name} could not be cast to {typeof(T).Name}.", error);
            }
        }

        internal static string Describe(Exception error)
        {
            var chain = new List<string>();
            for (var current = error; current != null; current = current.InnerException)
                chain.Add(current.GetType().Name + ": " + current.Message);
            return string.Join(" -> ", chain);
        }

        private static object Invoke(MethodInfo method, object target, string stage, params object[] args)
        {
            try { return method.Invoke(target, args); }
            catch (Exception error) { throw new InvalidDataException(stage + " reflection invocation failed.", error); }
        }

        private static MethodInfo Require(Type type, string name, params Type[] signature)
            => type.GetMethod(name, signature) ?? throw new MissingMethodException(type.FullName, name);

        public void Dispose()
        {
            if (_bundle == null) return;
            bool alive = IsAlive;
            var bundle = _bundle;
            _bundle = null;
            Fonts.Clear();
            _fontNames.Clear();
            _resources.Clear();
            if (alive) _unload.Invoke(bundle, new object[] { true });
        }
    }
}
#endif
