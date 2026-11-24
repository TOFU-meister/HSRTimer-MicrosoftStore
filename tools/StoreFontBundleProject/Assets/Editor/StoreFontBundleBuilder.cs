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
        if (mode != "embedded" && mode != "os" && mode != "static")
            throw new ArgumentException("-fontMode must be embedded, os or static.");
        string project = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.GetFullPath(Path.Combine(project, Option("-fontOutput", "Build/" + mode)));
        Directory.CreateDirectory(output);
        var builds = new List<AssetBundleBuild>();
        var bundleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.GetFiles(Path.Combine(project, FontFolder)))
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".ttf" && extension != ".otf") continue;
            string asset = FontFolder + "/" + Path.GetFileName(path);
            var importer = AssetImporter.GetAtPath(asset) as TrueTypeFontImporter;
            if (importer == null) throw new InvalidOperationException("Unity did not import a Font: " + asset);
            importer.fontTextureCase = mode == "static" ? FontTextureCase.CustomSet : FontTextureCase.Dynamic;
            importer.includeFontData = mode == "embedded";
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
        var manifest = BuildPipeline.BuildAssetBundles(output, builds.ToArray(),
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
            BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new InvalidOperationException("Unity did not build the AssetBundles.");
        foreach (var build in builds) Debug.Log("Bundle: " + Path.Combine(output, build.assetBundleName));
    }

    private static string Option(string name, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }
}
