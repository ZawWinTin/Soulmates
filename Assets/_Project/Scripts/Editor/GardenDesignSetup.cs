#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Installs the garden presentation through Unity's asset/scene APIs. Existing UI is retained inactive.</summary>
public static class GardenDesignSetup
{
    const string Art = "Assets/_Project/Garden";

    static Sprite[] ImportPinkExpressions()
    {
        string path = Art + "/pink-expression-sheet.png";
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 132;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritesheet = new[]
        {
            new SpriteMetaData
            {
                name = "PinkFront",
                rect = new Rect(16, 0, 887, 887),
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(.5f, .3f),
            },
            new SpriteMetaData
            {
                name = "PinkBlink",
                rect = new Rect(866, 0, 887, 887),
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(.5f, .3f),
            },
        };
        importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
    }

    [MenuItem("Tools/Soulmates/Garden/Install design")]
    public static void Install()
    {
        if (!AssetDatabase.IsValidFolder(Art))
            AssetDatabase.CreateFolder("Assets/_Project", "Garden");
        // Neutral white nine-slice: geometry belongs to the UI system, not a baked colored button.
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var pixels = new Color[64 * 64];
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            float dx = Mathf.Max(14 - x, x - 49),
                dy = Mathf.Max(14 - y, y - 49);
            float distance = new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude;
            pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Clamp01(14 - distance));
        }
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(Art + "/rounded.png", texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(Art + "/rounded.png");
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art + "/rounded.png");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteBorder = new Vector4(16, 16, 16, 16);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        var theme = AssetDatabase.LoadAssetAtPath<GardenTheme>(Art + "/GardenTheme.asset");
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<GardenTheme>();
            AssetDatabase.CreateAsset(theme, Art + "/GardenTheme.asset");
        }
        theme.illustration = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Sprites/UI/bg_menu.png"
        );
        theme.logo = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Sprites/UI/ui_logo_3.png"
        );
        theme.panel = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Sprites/UI/ui_panel.png"
        );
        theme.button = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Sprites/UI/ui_btn.png"
        );
        theme.keycap = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Sprites/UI/key_cap.png"
        );
        theme.ground = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Sprites/tile_grass_1.png"
        );
        theme.heart = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Sprites/UI/icon_heart.png"
        );
        theme.helpIcon = ImportPolishedSprite("help.png", false);
        theme.pauseIcon = ImportPolishedSprite("pause.png", false);
        theme.arrow = ImportPolishedSprite("arrow-up.png", false);
        theme.sparkle = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Sprites/UI/sparkle.png"
        );
        theme.retryLogo = ImportPolishedSprite("soulmates-icon.png", false);
        var appIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/soulmates-icon.png");
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { appIcon });
        foreach (var target in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.WebGL })
        {
            int count = PlayerSettings.GetIconSizesForTargetGroup(target).Length;
            if (count > 0)
                PlayerSettings.SetIconsForTargetGroup(
                    target,
                    Enumerable.Repeat(appIcon, count).ToArray()
                );
        }
        theme.tutorial = ImportPolishedSprite("tutorial-v2.png", false);
        theme.button = ImportPolishedSprite("marshmallow-button-v2.png", true);
        theme.softCard = theme.button;
        theme.rounded = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/rounded.png");
        theme.star = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Sprites/UI/star_3d.png"
        );
        theme.boy = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Sprites/Characters/char_boy_front.png"
        );
        theme.girl = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Sprites/Characters/char_girl_front.png"
        );
        var pinkExpressions = ImportPinkExpressions();
        theme.girl = pinkExpressions.First(sprite => sprite.name == "PinkFront");
        theme.body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/_Project/Fonts/Roboto-Medium SDF.asset"
        );
        theme.heading = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/_Project/Fonts/Roboto-Bold SDF.asset"
        );
        theme.headingMaterial = CleanMaterial(theme.heading.material, "Heading");
        theme.bodyMaterial = CleanMaterial(theme.body.material, "Body");
        EditorUtility.SetDirty(theme);
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
                if (c.GetComponentInParent<GardenInterface>() == null)
                    c.gameObject.SetActive(false);
            foreach (var old in UnityEngine.Object.FindObjectsOfType<PlayOptions>(true))
                old.enabled = false;
            foreach (var old in UnityEngine.Object.FindObjectsOfType<PauseMenu>(true))
                old.enabled = false;
            foreach (var old in UnityEngine.Object.FindObjectsOfType<CameraController>(true))
                old.enabled = false;
            foreach (var player in UnityEngine.Object.FindObjectsOfType<PlayerController>(true))
            {
                if (
                    player.frontSprite == null
                    || !(
                        player.frontSprite.name.Contains("girl")
                        || player.frontSprite.name == "PinkFront"
                    )
                )
                    continue;
                player.frontSprite = theme.girl;
                player.blinkSprite = pinkExpressions.First(sprite => sprite.name == "PinkBlink");
                var renderer = player.GetComponent<SpriteRenderer>();
                renderer.sprite = player.frontSprite;
                EditorUtility.SetDirty(player);
                EditorUtility.SetDirty(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            var camera = Camera.main;
            if (camera != null)
            {
                foreach (Transform child in camera.transform)
                    if (child.name == "SkyBackground" || child.name == "Clouds")
                        child.gameObject.SetActive(true);
                camera.backgroundColor = new Color(.91f, .93f, .88f);
            }
            foreach (var star in UnityEngine.Object.FindObjectsOfType<StarPickup>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(star.gameObject);
            var ui = UnityEngine.Object.FindObjectOfType<GardenInterface>();
            if (ui == null)
                ui = new GameObject("Garden Presentation").AddComponent<GardenInterface>();
            ui.theme = theme;
            var gc = UnityEngine.Object.FindObjectOfType<GameController>();
            if (gc != null)
            {
                var signal = scene
                    .GetRootGameObjects()
                    .FirstOrDefault(g => g.name == "Completion Signal");
                if (signal == null)
                    signal = new GameObject("Completion Signal");
                foreach (
                    var duplicate in scene
                        .GetRootGameObjects()
                        .Where(g => g.name == "Completion Signal" && g != signal)
                )
                    UnityEngine.Object.DestroyImmediate(duplicate);
                signal.SetActive(false);
                gc.completeLevelUI = signal;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Menu.unity");
        AssetDatabase.SaveAssets();
        File.WriteAllText(
            "Design/install-result.txt",
            "Garden design installed in all 11 build scenes.\n"
        );
        Debug.Log("Garden design installed.");
    }

    static Sprite ImportPolishedSprite(string name, bool button)
    {
        string path = Art + "/" + name;
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        if (button)
        {
            // Trim transparent padding in sprite metadata, preserving the generated PNG.
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = new[]
            {
                new SpriteMetaData
                {
                    name = "Marshmallow button",
                    rect = new Rect(70, 105, 2032, 510),
                    pivot = new Vector2(.5f, .5f),
                    alignment = (int)SpriteAlignment.Center,
                    border = new Vector4(270, 110, 270, 110),
                },
            };
        }
        else
            importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
    }

    static Material CleanMaterial(Material original, string name)
    {
        string path = Art + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(original);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.DisableKeyword("UNDERLAY_ON");
        mat.DisableKeyword("UNDERLAY_INNER");
        mat.DisableKeyword("OUTLINE_ON");
        mat.SetFloat("_OutlineWidth", 0);
        mat.SetFloat("_FaceDilate", 0);
        mat.SetColor("_FaceColor", Color.white);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    [MenuItem("Tools/Soulmates/Garden/Build native review")]
    public static void InstallAndBuild()
    {
        Install();
        BuildPreview();
    }

    [MenuItem("Tools/Soulmates/Garden/Build all reviews")]
    public static void BuildReviewPackage()
    {
        Install();
        BuildPreview();
        BuildWebPreview();
    }

    [MenuItem("Tools/Soulmates/Garden/Build WebGL review")]
    public static void BuildWebPreview()
    {
        Install();
        // Local review builds favor iteration time. Restore the author's publishing preset afterward.
        var settings = Type.GetType(
            "UnityEditor.WebGL.UserBuildSettings, UnityEditor.WebGL.Extensions"
        );
        var property = settings?.GetProperty(
            "codeOptimization",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        );
        var previous = property?.GetValue(null);
        try
        {
            if (property != null)
                property.SetValue(null, Enum.Parse(property.PropertyType, "BuildTimes"));
            var result = UnityEditor.BuildPipeline.BuildPlayer(
                EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                "Design/WebGL/soulmates",
                BuildTarget.WebGL,
                BuildOptions.None
            );
            File.WriteAllText(
                "Design/webgl-result.txt",
                result.summary.result + " errors=" + result.summary.totalErrors
            );
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception("WebGL build failed");
        }
        finally
        {
            if (property != null && previous != null)
                property.SetValue(null, previous);
        }
    }

    public static void BuildPreview()
    {
        var result = UnityEditor.BuildPipeline.BuildPlayer(
            EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            "Design/Preview/Soulmates.app",
            BuildTarget.StandaloneOSX,
            BuildOptions.Development
        );
        File.WriteAllText(
            "Design/build-result.txt",
            result.summary.result + " errors=" + result.summary.totalErrors
        );
        if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("Preview build failed");
    }
}
#endif
