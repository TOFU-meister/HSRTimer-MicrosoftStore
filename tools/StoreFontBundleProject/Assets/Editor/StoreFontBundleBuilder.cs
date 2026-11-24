using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class StoreFontBundleBuilder
{
    private const string FontFolder = "Assets/Fonts";
    private const string SampleCharacters = " 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"
        + "!?%+-:.,/()[]日本語あいうえおアイウエオ記録更新简体中文繁體计时记录";

    public static void BuildIsolatedFont()
    {
        string mode = Option("-fontMode", "embedded");
        if (mode != "embedded" && mode != "embedded-raw")
            throw new ArgumentException("Isolated fonts must embed the source font.");
        if (Option("-fontTarget", "StandaloneWindows64") != "StandaloneWindows64")
            throw new ArgumentException("Isolated fonts require StandaloneWindows64.");
        if (Directory.GetFiles(Path.Combine(Application.dataPath, "Fonts")).Count(path =>
            string.Equals(Path.GetExtension(path), ".ttf", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetExtension(path), ".otf", StringComparison.OrdinalIgnoreCase)) != 1)
            throw new InvalidDataException("The isolated project must contain exactly one source font.");
        BuildCore(requireSingleFont: true);
    }

    public static void VerifyIsolatedFonts()
    {
        if (Application.unityVersion != "2019.4.24f1")
            throw new InvalidOperationException("Use Unity Editor 2019.4.24f1 to match the Store player.");

        string directory = Option("-fontOutput", "");
        string[] paths = Directory.GetFiles(directory, "*.bundle");
        if (paths.Length == 0) throw new InvalidDataException("No bundles to verify in " + directory);

        var opened = new List<AssetBundle>();
        var fontIds = new HashSet<int>();
        var loadedFonts = new List<Font>();
        try
        {
            foreach (string path in paths)
            {
                if (new FileInfo(path).Length > 32L * 1024 * 1024)
                    throw new InvalidDataException("Bundle exceeds the game's 32 MiB limit: " + path);
                var bundle = Open(path);
                opened.Add(bundle);
                var fonts = bundle.LoadAllAssets<Font>();
                if (fonts.Length != 1 || fonts[0] == null || !fonts[0].dynamic)
                    throw new InvalidDataException("Expected exactly one Dynamic Font in " + path);

                var font = fonts[0];
                if (!fontIds.Add(font.GetInstanceID()))
                    throw new InvalidDataException("Another bundle returned the same Font instance: " + path);
                const string digits = "0123456789";
                loadedFonts.Add(font);
                string sample = Path.GetFileName(path).StartsWith("notosanscjk", StringComparison.OrdinalIgnoreCase)
                    ? SampleCharacters : digits;
                font.RequestCharactersInTexture(sample, 18);
                if (sample.Any(character => !font.HasCharacter(character)) ||
                    font.material == null || font.material.mainTexture == null)
                    throw new InvalidDataException("Missing sample glyphs or Font atlas in " + path);
                Debug.Log($"Concurrent validation: {Path.GetFileName(path)} | Font={font.name} | fonts={fonts.Length}"
                    + $" | materials={bundle.LoadAllAssets<Material>().Length} | textures={bundle.LoadAllAssets<Texture2D>().Length}"
                    + $" | glyphs={sample.Length}/{sample.Length}");
            }
            if (loadedFonts.Any(font => font == null || font.material == null || font.material.mainTexture == null))
                throw new InvalidDataException("A Font or atlas was lost while opening another bundle.");
            Debug.Log($"Editor verified {opened.Count} isolated bundles open together.");
        }
        finally
        {
            foreach (var bundle in opened)
                bundle.Unload(true);
        }
    }

    [MenuItem("Tools/HSRTimer/Build embedded font bundles")]
    public static void Build() => BuildCore(requireSingleFont: false);

    private static void BuildCore(bool requireSingleFont)
    {
        if (Application.unityVersion != "2019.4.24f1")
            throw new InvalidOperationException("Use Unity Editor 2019.4.24f1 to match the Store player.");

        string mode = Option("-fontMode", "embedded");
        if (mode != "embedded" && mode != "embedded-raw" && mode != "os" && mode != "static" && mode != "texture")
            throw new ArgumentException("-fontMode must be embedded, embedded-raw, os, static or texture.");
        string project = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.GetFullPath(Path.Combine(project, Option("-fontOutput", "Build/" + mode)));
        Directory.CreateDirectory(output);
        var builds = new List<AssetBundleBuild>();
        if (mode == "texture")
        {
            const string controlPath = FontFolder + "/HSRTimerControl.asset";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(controlPath) == null)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels(new[] { Color.white, Color.black, Color.black, Color.white });
                texture.Apply();
                AssetDatabase.CreateAsset(texture, controlPath);
                AssetDatabase.SaveAssets();
            }
            builds.Add(new AssetBundleBuild
            {
                assetBundleName = "hsrtimer-texture-control.bundle",
                assetNames = new[] { controlPath },
            });
        }
        var bundleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] paths = Directory.GetFiles(Path.Combine(project, FontFolder));
        Array.Sort(paths, StringComparer.Ordinal);
        foreach (string path in paths)
        {
            if (mode == "texture") break;
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".ttf" && extension != ".otf") continue;
            string asset = FontFolder + "/" + Path.GetFileName(path);
            var importer = AssetImporter.GetAtPath(asset) as TrueTypeFontImporter;
            if (importer == null) throw new InvalidOperationException("Unity did not import a Font: " + asset);
            importer.fontTextureCase = mode == "static" ? FontTextureCase.CustomSet : FontTextureCase.Dynamic;
            importer.includeFontData = mode == "embedded" || mode == "embedded-raw";
            importer.fontSize = 18;
            importer.customCharacters = mode == "static" ? SampleCharacters : "";
            importer.fontNames = mode == "os" ? new[] { importer.fontTTFName } : Array.Empty<string>();
            importer.SaveAndReimport();
            string[] subAssets = AssetDatabase.LoadAllAssetsAtPath(asset)
                .Select(item => item.GetType().Name + ":" + item.name).ToArray();
            Debug.Log($"Imported {asset} | embed={importer.includeFontData} | font names=[{string.Join(", ", importer.fontNames)}]"
                + $" | sub-assets=[{string.Join(", ", subAssets)}]");
            var font = AssetDatabase.LoadAssetAtPath<Font>(asset);
            if (font == null || font.dynamic != (mode != "static"))
                throw new InvalidOperationException("Imported Font is invalid or has an unexpected mode: " + asset);
            string name = Path.GetFileNameWithoutExtension(asset).ToLowerInvariant() + "-" + mode + ".bundle";
            if (!bundleNames.Add(name)) throw new InvalidOperationException("Duplicate bundle name: " + name);
            builds.Add(new AssetBundleBuild { assetBundleName = name, assetNames = new[] { asset } });
        }

        if (builds.Count == 0) throw new InvalidOperationException("Place a licensed .ttf or .otf under " + FontFolder);
        string targetName = Option("-fontTarget", "StandaloneWindows64");
        if (targetName != "StandaloneWindows64" && targetName != "WSAPlayer")
            throw new ArgumentException("-fontTarget must be StandaloneWindows64 or WSAPlayer.");
        var target = (BuildTarget)Enum.Parse(typeof(BuildTarget), targetName);
        var compression = mode == "embedded-raw" ? BuildAssetBundleOptions.UncompressedAssetBundle
            : BuildAssetBundleOptions.ChunkBasedCompression;
        var options = compression | BuildAssetBundleOptions.ForceRebuildAssetBundle;
        if (builds.Count > 1)
        {
            // Retain the combined manifest to show any cross-bundle edges Unity infers.
            string diagnostics = Path.Combine(output, "combined-diagnostics");
            var combined = BuildBundles(diagnostics, builds.ToArray(), options, target);
            foreach (var build in builds)
                LogDependencies(combined, build.assetBundleName, "Combined");
        }

        var pathsToVerify = new List<string>();
        foreach (var build in builds)
        {
            foreach (string dependency in AssetDatabase.GetDependencies(build.assetNames[0], false))
                if (dependency != build.assetNames[0])
                    Debug.Log($"Imported dependency for {build.assetNames[0]}: {dependency}");

            // Unity can place shared dependencies in other bundles in a combined build.
            // Build one named bundle at a time so its implicit dependencies are included.
            string isolated = Path.Combine(output, "individual", Path.GetFileNameWithoutExtension(build.assetBundleName));
            var manifest = BuildBundles(isolated, new[] { build }, options, target);
            var manifestNames = manifest.GetAllAssetBundles();
            if (manifestNames.Length != 1 || manifestNames[0] != build.assetBundleName)
                throw new InvalidDataException($"Unexpected bundles in {isolated}: [{string.Join(", ", manifestNames)}]");
            if (LogDependencies(manifest, build.assetBundleName, "Individual"))
                throw new InvalidDataException("External bundle dependencies are not supported: " + isolated);

            string path = Path.Combine(isolated, build.assetBundleName);
            if (new FileInfo(path).Length > 32L * 1024 * 1024)
                throw new InvalidDataException("Bundle exceeds the game's 32 MiB limit: " + path);

            var standalone = Open(path);
            try
            {
                Verify(standalone, path, build.assetNames[0], mode);
                if (requireSingleFont && standalone.LoadAllAssets<Font>().Length != 1)
                    throw new InvalidDataException("Unexpected additional Font assets in " + path);
            }
            finally { standalone.Unload(true); }
            Debug.Log("Editor verified standalone: " + build.assetBundleName);
            pathsToVerify.Add(path);
        }

        var openBundles = new List<AssetBundle>();
        try
        {
            for (int i = 0; i < builds.Count; i++)
            {
                string path = pathsToVerify[i];
                var bundle = Open(path);
                openBundles.Add(bundle);
                Verify(bundle, path, builds[i].assetNames[0], mode);
            }
            Debug.Log($"Editor verified {openBundles.Count} bundles open together without dependencies.");
        }
        finally
        {
            foreach (var bundle in openBundles)
                bundle.Unload(true);
        }

        for (int i = 0; i < builds.Count; i++)
        {
            string destination = Path.Combine(output, builds[i].assetBundleName);
            File.Copy(pathsToVerify[i], destination, true);
            File.Copy(pathsToVerify[i] + ".manifest", destination + ".manifest", true);
        }
    }

    private static AssetBundleManifest BuildBundles(string directory, AssetBundleBuild[] builds,
        BuildAssetBundleOptions options, BuildTarget target)
    {
        Directory.CreateDirectory(directory);
        var manifest = BuildPipeline.BuildAssetBundles(directory, builds, options, target);
        if (manifest == null) throw new InvalidOperationException("Unity did not build the AssetBundles: " + directory);
        return manifest;
    }

    private static bool LogDependencies(AssetBundleManifest manifest, string name, string phase)
    {
        string[] direct = manifest.GetDirectDependencies(name);
        string[] all = manifest.GetAllDependencies(name);
        string[] indirect = all.Except(direct, StringComparer.Ordinal).ToArray();
        Debug.Log($"{phase} dependencies for {name} | direct=[{string.Join(", ", direct)}] | indirect=[{string.Join(", ", indirect)}]");
        return direct.Length != 0 || all.Length != 0;
    }

    private static AssetBundle Open(string path)
    {
        var bundle = AssetBundle.LoadFromFile(path, 0, 0);
        if (bundle == null) throw new InvalidDataException("Editor could not open bundle: " + path);
        return bundle;
    }

    private static void Verify(AssetBundle bundle, string path, string expectedAsset, string mode)
    {
        var names = bundle.GetAllAssetNames();
        if (names.Length != 1 || !string.Equals(names[0], expectedAsset, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unexpected asset name or count in " + path);
        Debug.Log("Editor opened " + path + " | asset=" + names[0]);
        if (mode == "texture")
        {
            var texture = bundle.LoadAsset<Texture2D>(names[0]);
            if (texture == null) throw new InvalidDataException("Control Texture2D could not be read.");
            Debug.Log($"Editor control: {texture.width} x {texture.height}");
            return;
        }
        var font = bundle.LoadAsset<Font>(names[0]);
        if (font == null || font.dynamic != (mode != "static"))
            throw new InvalidDataException("Font not readable or unexpected dynamic flag.");
        if (font.dynamic) font.RequestCharactersInTexture("Aa0日本語", 18);
        var material = font.material;
        var atlas = material == null ? null : material.mainTexture;
        Debug.Log($"Editor Font: {font.name}; dynamic={font.dynamic}; A={font.HasCharacter('A')}; "
            + $"日={font.HasCharacter('日')}; material={material?.name ?? "missing"}; atlas={(atlas == null ? "missing" : atlas.width + "x" + atlas.height)}");
    }

    private static string Option(string name, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }
}
