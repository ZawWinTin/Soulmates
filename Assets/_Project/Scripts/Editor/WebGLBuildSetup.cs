using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// One-click WebGL setup + build for hosting Soulmates on z-games.
/// Written by Mei (2026-07-27). Run from the Tools menu — never from MCP.
///
/// Tools ▸ Soulmates ▸ WebGL: 1. Configure For z-games
///   Sets every Player/Build setting the z-games embed expects.
/// Tools ▸ Soulmates ▸ WebGL: 2. Build For z-games
///   Builds straight into z-games/public/_builds/soulmates (folder name
///   controls the output filenames — must stay lowercase "soulmates").
/// </summary>
public static class WebGLBuildSetup
{
    private const string OutputDir =
        "/Users/zawwintin/Developer/Projects/Websites/Herd/z-games/public/_builds/soulmates";

    private const string IconPath = "Assets/_Project/Sprites/UI/app_icon.png";

    [MenuItem("Tools/Soulmates/WebGL: 1. Configure For z-games")]
    public static void Configure()
    {
        // --- Player ▸ Resolution and Presentation ---
        PlayerSettings.defaultWebScreenWidth = 960;
        PlayerSettings.defaultWebScreenHeight = 600;
        PlayerSettings.runInBackground = true;

        // --- Player ▸ Publishing Settings ---
        // Disabled = plain filenames (soulmates.data/.wasm/...), zero server
        // config needed. Switch to Brotli later when z-core streams builds.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.template = "APPLICATION:Default";

        // --- Default Icon (used by future desktop builds; harmless for web) ---
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon != null)
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
        else
            Debug.LogWarning($"[WebGLBuildSetup] Icon not found at {IconPath} — skipped.");

        // --- Build Settings ▸ Code Optimization → Disk Size With LTO ---
        // (Reflection: UnityEditor.WebGL.UserBuildSettings only exists when
        // the WebGL module is installed; avoids hard compile dependency.)
        TrySetCodeOptimization("DiskSizeLTO");

        // --- Sanity: Menu scene must be first ---
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        if (scenes.Length == 0 || !scenes[0].path.Contains("Menu"))
            Debug.LogWarning("[WebGLBuildSetup] First enabled scene is not Menu — check Scenes In Build order!");

        AssetDatabase.SaveAssets();
        Debug.Log("[WebGLBuildSetup] Configure done: 960x600, run-in-background, "
                  + "compression Disabled, data caching on, icon set, DiskSizeLTO. "
                  + $"Scenes in build: {scenes.Length}.");
    }

    [MenuItem("Tools/Soulmates/WebGL: 2. Build For z-games")]
    public static void Build()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            Debug.Log("[WebGLBuildSetup] Switching active build target to WebGL first (this reimports — be patient)…");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError("[WebGLBuildSetup] Could not switch to WebGL. Is WebGL Build Support installed?");
                return;
            }
        }

        Directory.CreateDirectory(OutputDir);

        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });

        var summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
            Debug.Log($"[WebGLBuildSetup] BUILD OK → {OutputDir} "
                      + $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalMinutes:F1} min). "
                      + "Tell Mei — she'll verify and wire the catalog.");
        else
            Debug.LogError($"[WebGLBuildSetup] Build {summary.result}: {summary.totalErrors} errors.");
    }

    private static void TrySetCodeOptimization(string value)
    {
        try
        {
            var t = Type.GetType("UnityEditor.WebGL.UserBuildSettings, UnityEditor.WebGL.Extensions");
            var prop = t?.GetProperty("codeOptimization", BindingFlags.Public | BindingFlags.Static);
            if (prop == null)
            {
                Debug.LogWarning("[WebGLBuildSetup] Could not set Code Optimization via API — "
                                 + "set it manually in Build Settings: Disk Size With LTO.");
                return;
            }
            var enumVal = Enum.Parse(prop.PropertyType, value);
            prop.SetValue(null, enumVal);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WebGLBuildSetup] Code Optimization not set ({e.Message}) — "
                             + "set it manually in Build Settings: Disk Size With LTO.");
        }
    }
}
