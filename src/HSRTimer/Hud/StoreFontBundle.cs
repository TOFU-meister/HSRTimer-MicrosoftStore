#if STORE_IL2CPP
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
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
        internal List<Font> Fonts { get; } = new();

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
                Status(type, "GetAllAssetNames"),
                Status(type, "LoadAsset", typeof(string), typeof(Il2CppSystem.Type)),
                Status(type, "Unload", typeof(bool)),
            });
        }

        private static string Status(Type type, string name, params Type[] signature)
            => $"{name}({string.Join(", ", Array.ConvertAll(signature, t => t.Name))})="
                + (type.GetMethod(name, signature) == null ? "missing" : "present");

        internal static StoreFontBundle Load(string path)
            => Load(path, FindApi(), () => Il2CppType.Of<Font>());

        internal static StoreFontBundle Load(string path, Type type, Func<Il2CppSystem.Type> fontType)
        {
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length < 8 || stream.Length > MaxFileBytes)
                    throw new InvalidDataException("Bundle must be at most 32 MiB and have a UnityFS header.");
                foreach (char expected in "UnityFS\0")
                    if (stream.ReadByte() != expected) throw new InvalidDataException("Expected a UnityFS AssetBundle.");
            }

            if (type == null) throw new NotSupportedException(Module + " is unavailable in this Store interop.");
            // Resolve all required wrapper methods before opening a native bundle.
            var load = type.GetMethod("LoadFromFile", new[] { typeof(string) })
                ?? Require(type, "LoadFromFile", typeof(string), typeof(uint), typeof(ulong));
            var names = Require(type, "GetAllAssetNames");
            var asset = Require(type, "LoadAsset", typeof(string), typeof(Il2CppSystem.Type));
            var unload = Require(type, "Unload", typeof(bool));
            var requestedType = fontType();
            var result = load.Invoke(null, load.GetParameters().Length == 1
                ? new object[] { path } : new object[] { path, 0u, 0ul });
            if (result == null || result is UnityEngine.Object unityBundle && unityBundle == null)
                throw new InvalidDataException("Unity rejected the bundle. Check Editor version and Windows target.");

            var loaded = new StoreFontBundle(result, unload);
            try
            {
                if (names.Invoke(result, null) is not IEnumerable assets)
                    throw new InvalidDataException("Unity returned no asset names.");
                int count = 0;
                foreach (string name in assets)
                {
                    if (++count > MaxAssets) throw new InvalidDataException("Bundle contains more than 32 named assets.");
                }
                foreach (string name in assets)
                {
                    var value = asset.Invoke(result, new object[] { name, requestedType });
                    var font = value as Font ?? (value as Il2CppObjectBase)?.TryCast<Font>();
                    if (font != null) loaded.Fonts.Add(font);
                }
                if (loaded.Fonts.Count == 0) throw new InvalidDataException("The bundle contains no readable UnityEngine.Font.");
                return loaded;
            }
            catch
            {
                loaded.Dispose();
                throw;
            }
        }

        private static MethodInfo Require(Type type, string name, params Type[] signature)
            => type.GetMethod(name, signature) ?? throw new MissingMethodException(type.FullName, name);

        public void Dispose()
        {
            if (_bundle == null) return;
            var bundle = _bundle;
            _bundle = null;
            Fonts.Clear();
            _unload.Invoke(bundle, new object[] { true });
        }
    }
}
#endif
