using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class StoreFontBundleBuilder
{
    private const string FontFolder = "Assets/Fonts";
    private const string SampleCharacters = " 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"
        + "+-:.,%()[]/日本語・ひらがなカタ漢字简体中文繁體";

    [MenuItem("Tools/HSRTimer/Build embedded font bundles")]
    public static void Build()
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
        foreach (string path in Directory.GetFiles(Path.Combine(project, FontFolder)))
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
        var manifest = BuildPipeline.BuildAssetBundles(output, builds.ToArray(),
            compression | BuildAssetBundleOptions.ForceRebuildAssetBundle, target);
        if (manifest == null) throw new InvalidOperationException("Unity did not build the AssetBundles.");
        foreach (var build in builds)
            Verify(Path.Combine(output, build.assetBundleName), mode);
    }

    private static void Verify(string path, string mode)
    {
        var bundle = AssetBundle.LoadFromFile(path, 0, 0);
        if (bundle == null) throw new InvalidDataException("Editor rejected newly generated bundle: " + path);
        try
        {
            var names = bundle.GetAllAssetNames();
            if (names.Length != 1) throw new InvalidDataException("Expected one main asset in " + path);
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
            var atlas = font.material == null ? null : font.material.mainTexture;
            Debug.Log($"Editor Font: {font.name}; dynamic={font.dynamic}; A={font.HasCharacter('A')}; "
                + $"日={font.HasCharacter('日')}; atlas={(atlas == null ? "missing" : atlas.width + "x" + atlas.height)}");
        }
        finally
        {
            bundle.Unload(true);
        }
    }

    private static string Option(string name, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }
}
