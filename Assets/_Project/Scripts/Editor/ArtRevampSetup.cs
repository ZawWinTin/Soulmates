#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-click wiring for the kawaii UI revamp. Run via the menu:
///   Tools ▸ Soulmates ▸ Apply Art Revamp (UI)
/// It (1) sets import settings + 9-slice borders on the new sprites and
/// (2) reskins Menu.unity: background, buttons, and a title logo.
/// Idempotent — safe to run more than once.
/// </summary>
public static class ArtRevampSetup
{
    const string UIDir = "Assets/_Project/Sprites/UI/";
    const string MenuScene = "Assets/_Project/Scenes/Menu.unity";
    const string CreditScene = "Assets/_Project/Scenes/Credit.unity";

    // ── Modern UI kit (sliced from for_claude/ui_kits.png → Sprites/UI) ─────────
    // Designed sprites, not tinted rects: a clean cream panel (ui_card), a coral pill button (ui_btn),
    // a round knob (ui_knob). This is the real UI look, shared by EVERY screen via AddCard/SkinButton.
    // The primitives just place these sprites + a soft shadow.
    static readonly Color CardShadowSoft = new Color(0.4f, 0.3f, 0.42f, 0.18f); // gentle float shadow
    static readonly Color TextPlum = new Color(0.46f, 0.24f, 0.34f); // deep wine-rose — warm, harmonizes with the coral accent
    static readonly Color Accent = new Color(1f, 0.46f, 0.6f); // coral-pink — sliders/active accents

    static Sprite CardSprite() =>
        AssetDatabase.LoadAssetAtPath<Sprite>(UIDir + "ui_card.png")
        ?? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

    static Sprite ButtonSprite() =>
        AssetDatabase.LoadAssetAtPath<Sprite>(UIDir + "ui_btn.png")
        ?? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

    static Sprite KnobSprite() =>
        AssetDatabase.LoadAssetAtPath<Sprite>(UIDir + "ui_knob.png")
        ?? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

    // ███ THE ONLY ONE YOU NEED ███ — runs everything in the right order so you don't have to pick
    // from the long list. Safe to re-run anytime (every step is idempotent). priority -100 pins it to
    // the top of the menu, above a separator.
    [MenuItem("Tools/Soulmates/▶ Apply EVERYTHING", priority = -100)]
    public static void ApplyEverything()
    {
        ApplyGroundTile(); // import + assign all grass-tile variants
        ApplyCharacters(); // slimes + win-tile tints
        ApplyHugStack(); // the hugging couple
        ApplyWinTileRune(); // goal-tile rune
        ApplyGameplayBackground(); // sky + drifting clouds
        ApplyLevelLighting(); // brighten the ground
        ApplyLoveSmoke(); // heart puff
        ApplyGameUI(); // pause + help
        ApplyWinScreen(); // level-complete
        Apply(); // menu + level select + options + transition (opens Menu scene)
        RebakeGroundTilesInAllScenes(); // bake the tiles into every scene
        Debug.Log("[ArtRevamp] ▶ Apply EVERYTHING complete — review the game.");
    }

    // Just the UI screens (faster than Apply EVERYTHING when you're only iterating on UI).
    [MenuItem("Tools/Soulmates/Apply UI Theme (All Screens)", priority = 20)]
    public static void ApplyUITheme()
    {
        Apply(); // importers, menu wiring (incl. play + options), level-select buttons, transition
        ApplyGameUI(); // pause + help (shared cards + candy buttons)
        ApplyWinScreen(); // level-complete screen
        Debug.Log(
            "[ArtRevamp] Soft Candy UI applied: menu, level select, play, options, pause, help, win. Review each."
        );
    }

    [MenuItem("Tools/Soulmates/Apply Art Revamp (UI)")]
    public static void Apply()
    {
        ConfigureImporters();
        WireMenu();
        ReskinLevelButtonPrefab();
        ImproveTransition();
        Debug.Log("[ArtRevamp] Done. Open Menu.unity and press Play to review.");
    }

    // In-game UI (Pause / Help / etc.) — edits the shared Game Canvas prefab, so it
    // applies to every level scene. Separate command so it won't switch your open scene.
    [MenuItem("Tools/Soulmates/Apply Game UI (Pause + Help)")]
    public static void ApplyGameUI()
    {
        const string path = "Assets/_Project/Prefabs/Game Canvas.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var btn = Load("ui_button.png");
        var rose = TextPlum; // kit heading/text colour — consistent across menu, options, pause, help, win

        int skinned = 0;
        foreach (var b in root.GetComponentsInChildren<Button>(true))
        {
            // leave the round HUD icons (the ? help and || pause buttons) alone
            if (b.name == "PauseButton" || b.name == "HelpButton")
                continue;
            SkinButton(b.gameObject, btn, false);
            skinned++;
        }

        // Wire PauseMenu.pauseContent → the buttons' container, so PauseGame() can force it
        // visible at runtime (the level scenes override it to inactive, which a prefab edit
        // can't beat).
        var resume = FindIn(root.transform, "ResumeButton");
        var pm = root.GetComponent<PauseMenu>();
        if (pm != null && resume != null && resume.parent != null)
            pm.pauseContent = resume.parent.gameObject;
        // keep the in-pause options sub-panel hidden until its button is pressed
        var optGroup = FindIn(root.transform, "OptionMenuGroup");
        if (optGroup != null)
            optGroup.gameObject.SetActive(false);

        // Reskin the round HUD buttons (? and ||) → soft pink circle, rose icon
        foreach (var hudName in new[] { "PauseButton", "HelpButton" })
        {
            var t = FindIn(root.transform, hudName);
            if (t == null)
                continue;
            if (t.TryGetComponent(out Image circ))
                circ.color = new Color(1f, 0.72f, 0.80f, 1f);
            var ic = FindIn(t, "Icon");
            if (ic != null && ic.TryGetComponent(out Image ico))
            {
                ico.color = new Color(0.72f, 0.24f, 0.40f, 1f);
                SetRoundIcon(
                    ico,
                    hudName == "HelpButton" ? "icon_help.png" : "icon_pause.png",
                    28f
                );
            }
            if (t.TryGetComponent(out Button hb))
                StyleHudButtonColors(hb);
        }

        // HUD row at the top-right: space Help · Retry · Pause evenly (they were only 80px apart, too
        // tight for a third). Add a Retry button (clone Pause for identical styling) that reloads the level.
        var pauseBtnT = FindIn(root.transform, "PauseButton");
        var helpBtnT = FindIn(root.transform, "HelpButton");
        if (pauseBtnT != null && helpBtnT != null)
        {
            var prt = pauseBtnT.GetComponent<RectTransform>();
            var hrt = helpBtnT.GetComponent<RectTransform>();
            prt.anchoredPosition = new Vector2(-55f, -50f); // rightmost
            hrt.anchoredPosition = new Vector2(-175f, -50f); // leftmost

            RemoveChild(pauseBtnT.parent.gameObject, "RetryButton");
            var retry = Object.Instantiate(pauseBtnT.gameObject, pauseBtnT.parent);
            retry.name = "RetryButton";
            // sit it right next to Pause in the hierarchy (not last) so the Pause/Win overlays still
            // cover it — Instantiate appends last, which left it floating on top of the win screen.
            retry.transform.SetSiblingIndex(pauseBtnT.GetSiblingIndex() + 1);
            var rrt = retry.GetComponent<RectTransform>();
            rrt.anchorMin = prt.anchorMin;
            rrt.anchorMax = prt.anchorMax;
            rrt.pivot = prt.pivot;
            rrt.sizeDelta = prt.sizeDelta;
            rrt.anchoredPosition = new Vector2(-115f, -50f); // between Help and Pause
            var ic = FindIn(retry.transform, "Icon");
            if (ic != null && ic.TryGetComponent(out Image rico))
            {
                rico.color = new Color(0.72f, 0.24f, 0.40f, 1f);
                SetRoundIcon(rico, "icon_retry.png", 28f); // centred + square → no squeeze
            }
            if (retry.TryGetComponent(out Button rbtn))
            {
                for (int i = rbtn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                    UnityEventTools.RemovePersistentListener(rbtn.onClick, i);
                StyleHudButtonColors(rbtn); // selected/nav fix → won't light up on its own
            }
            if (retry.GetComponent<RestartButton>() == null)
                retry.AddComponent<RestartButton>();
        }

        // ── Time-star meter (top-left): a gold star + a bar that DRAINS toward the level's time par ──
        RemoveChild(root.gameObject, "LevelTimer");
        var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        var pbForCircle = FindIn(root.transform, "PauseButton");
        Sprite circleSprite =
            pbForCircle != null && pbForCircle.TryGetComponent(out Image pcImg)
                ? pcImg.sprite
                : null;
        var timerGo = new GameObject("LevelTimer", typeof(RectTransform), typeof(LevelTimer));
        timerGo.transform.SetParent(root.transform, false);
        var ttrt = timerGo.GetComponent<RectTransform>();
        ttrt.anchorMin = ttrt.anchorMax = new Vector2(0f, 1f);
        ttrt.pivot = new Vector2(0f, 1f);
        ttrt.anchoredPosition = new Vector2(18f, -16f);
        ttrt.sizeDelta = new Vector2(196f, 46f);

        // drain bar FIRST (its left end tucks BEHIND the star circle → the circle sits on the bar).
        // Slim candy pill like the options sliders: soft track + a warm fill inset so the track reads
        // as a thin frame. Height is set so pixelsPerUnitMultiplier gives a true pill (fully-round ends).
        var track = MakeImage(timerGo.transform, "Track", rounded, new Color(0.93f, 0.87f, 0.89f));
        var trkr = track.GetComponent<RectTransform>();
        trkr.anchorMin = new Vector2(0f, 0.5f);
        trkr.anchorMax = new Vector2(1f, 0.5f);
        trkr.pivot = new Vector2(0.5f, 0.5f);
        trkr.offsetMin = new Vector2(23f, -9f); // slimmer; starts under the circle
        trkr.offsetMax = new Vector2(-2f, 9f);
        var trackImg = track.GetComponent<Image>();
        trackImg.type = Image.Type.Sliced;
        trackImg.pixelsPerUnitMultiplier = 0.6f; // rounder pill (like the sliders)
        // gold fill (matches the star) so the bar reads as "the star's energy" draining away.
        // SLICED, not Filled: Filled stretches the rounded sprite and pixelates the caps; a 9-slice
        // stays crisp. LevelTimer drains it by moving the RIGHT anchor (anchorMax.x) so the pill just
        // gets shorter with clean rounded ends.
        var starGold = new Color(1f, 0.88f, 0.38f); // brighter, more luminous gold
        var fillGo = MakeImage(track.transform, "Fill", rounded, starGold);
        var fr = fillGo.GetComponent<RectTransform>();
        fr.anchorMin = new Vector2(0f, 0.5f);
        fr.anchorMax = new Vector2(1f, 0.5f);
        fr.pivot = new Vector2(0f, 0.5f);
        fr.offsetMin = new Vector2(3f, -6f);
        fr.offsetMax = new Vector2(0f, 6f);
        var fillImg = fillGo.GetComponent<Image>();
        fillImg.type = Image.Type.Sliced;
        fillImg.pixelsPerUnitMultiplier = 0.6f;

        // small KNOB that rides the draining edge (LevelTimer moves it via anchors) + hosts the
        // sparkle emitter. White dot in a gold ring, matching the star; kept small this time.
        var knobRing = MakeImage(track.transform, "Handle", circleSprite, starGold);
        var kr = knobRing.GetComponent<RectTransform>();
        kr.anchorMin = kr.anchorMax = new Vector2(1f, 0.5f);
        kr.pivot = new Vector2(0.5f, 0.5f);
        kr.sizeDelta = new Vector2(14f, 14f);
        knobRing.GetComponent<Image>().preserveAspect = true;
        var knobDot = MakeImage(knobRing.transform, "Dot", circleSprite, Color.white);
        var kdr = knobDot.GetComponent<RectTransform>();
        kdr.anchorMin = Vector2.zero;
        kdr.anchorMax = Vector2.one;
        kdr.offsetMin = new Vector2(3f, 3f);
        kdr.offsetMax = new Vector2(-3f, -3f);
        knobDot.GetComponent<Image>().preserveAspect = true;

        // sparkles emitted from the leading edge for a win-tile shimmer. Uses the UI-based emitter
        // (a world ParticleSystem won't render in this Screen-Space canvas).
        var spGo = new GameObject("Sparkles", typeof(RectTransform), typeof(UISparkles));
        spGo.transform.SetParent(knobRing.transform, false);
        spGo.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        var sprk = spGo.GetComponent<UISparkles>();
        sprk.sparkleSprite = Load("sparkle.png");
        sprk.color = new Color(1f, 0.92f, 0.55f, 1f); // warm gold twinkle

        // gold border RING behind the white star-circle, so the star's home reads as a framed badge
        var tring = MakeImage(timerGo.transform, "CircleBorder", circleSprite, starGold);
        var trr = tring.GetComponent<RectTransform>();
        trr.anchorMin = trr.anchorMax = new Vector2(0f, 0.5f);
        trr.pivot = new Vector2(0f, 0.5f);
        trr.sizeDelta = new Vector2(50f, 50f);
        trr.anchoredPosition = new Vector2(-2f, 0f);
        tring.GetComponent<Image>().preserveAspect = true;

        // WHITE circle behind the star (star is yellow — a white bg reads better than pink), ON TOP
        // of the bar's left end and inside the gold ring.
        var tcirc = MakeImage(timerGo.transform, "Circle", circleSprite, Color.white);
        var tcr = tcirc.GetComponent<RectTransform>();
        tcr.anchorMin = tcr.anchorMax = new Vector2(0f, 0.5f);
        tcr.pivot = new Vector2(0f, 0.5f);
        tcr.sizeDelta = new Vector2(46f, 46f);
        tcr.anchoredPosition = new Vector2(0f, 0f);
        tcirc.GetComponent<Image>().preserveAspect = true;
        var tstar = MakeImage(
            timerGo.transform,
            "ParStar",
            Load("star_3d.png") ?? Load("icon_star.png"),
            Color.white
        );
        var tsr = tstar.GetComponent<RectTransform>();
        tsr.anchorMin = tsr.anchorMax = new Vector2(0f, 0.5f);
        tsr.pivot = new Vector2(0.5f, 0.5f); // CENTER pivot → flips in place (not from its edge)
        tsr.sizeDelta = new Vector2(34f, 34f); // a touch bigger to soften minification
        tsr.anchoredPosition = new Vector2(23f, 0f); // centered on the 46px white circle
        tstar.GetComponent<Image>().preserveAspect = true;
        if (tstar.GetComponent<UIFlipX>() == null)
            tstar.AddComponent<UIFlipX>().period = 3f; // slow, dwells on the full star

        var lt = timerGo.GetComponent<LevelTimer>();
        lt.fillBar = fillImg;
        lt.parStar = tstar.GetComponent<Image>();
        lt.handle = kr;
        timerGo.transform.SetSiblingIndex(FindIn(root.transform, "PauseButton").GetSiblingIndex());

        // Build the (empty) Help screen's content + reliably wire open/close
        var help = FindIn(root.transform, "HelpScreen");
        if (help != null)
        {
            if (help.TryGetComponent(out Image helpBg))
                helpBg.color = new Color(0f, 0f, 0f, 0.45f); // dim gameplay like pause
            if (help.GetComponent<PauseWhileActive>() == null)
                help.gameObject.AddComponent<PauseWhileActive>(); // freeze game while open
            BuildHelpContent(help.gameObject);
            RewireToggle(FindIn(root.transform, "HelpButton"), help.gameObject, true);
            RewireToggle(FindIn(help, "BackButton"), help.gameObject, false);
            // Let Esc close Help instead of also opening Pause (the duplicate-panel bug).
            if (pm != null)
                pm.helpScreen = help.gameObject;
        }

        // Pause: add a cream card behind the buttons, INSIDE the buttons container so it only
        // shows when paused (the PauseMenu overlay is technically active at level start with
        // alpha 0, so a card placed there would show through). Clean up any old misplaced card.
        var pauseMenu = FindIn(root.transform, "PauseMenu");
        if (pauseMenu != null)
            RemoveChild(pauseMenu.gameObject, "Card");
        if (resume != null && resume.parent != null)
            AddCard(resume.parent, "Card", new Vector2(0.32f, 0.17f), new Vector2(0.68f, 0.83f));

        // Pause "Menu" + win "Exit to Menu": the level scenes OVERRIDE these buttons' serialized
        // onClick, so a prefab onClick edit can't win. Instead attach GoToMenuButton — a NEW component
        // (additions DO propagate to scene instances) that drives PauseMenu.LoadMenu from code.
        foreach (var bn in new[] { "MenuButton", "ExitMenuButton" })
        {
            var bt = FindIn(root.transform, bn);
            if (
                bt != null
                && bt.GetComponent<Button>() != null
                && bt.GetComponent<GoToMenuButton>() == null
            )
                bt.gameObject.AddComponent<GoToMenuButton>();
        }

        // Style the in-pause Options sub-panel to match the menu Options (card + plum title + candy
        // sliders + coral Back). Its buttons keep their existing show/hide wiring.
        var optionsFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/_Project/Fonts/Roboto-Medium SDF.asset"
        );
        if (optGroup != null)
            StylePauseOptions(optGroup, optionsFont);

        // Smooth open transitions (soft pop+fade) for the in-game overlays — consistent with the menu.
        if (help != null)
            EnsurePopIn(help.gameObject, FindIn(help, "HelpCard"));
        if (resume != null && resume.parent != null)
            EnsurePopIn(resume.parent.gameObject, resume.parent); // pause buttons group
        if (optGroup != null)
            EnsurePopIn(optGroup.gameObject, FindIn(optGroup, "OptCard"));

        foreach (var t in root.GetComponentsInChildren<Text>(true))
            t.color = rose;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            t.color = rose;

        // ...but BUTTON LABELS must stay WHITE on the coral pills (the blanket recolor above turned them
        // dark plum → unreadable). Re-whiten them last, skipping the round HUD icons.
        foreach (var b in root.GetComponentsInChildren<Button>(true))
        {
            if (b.name == "PauseButton" || b.name == "HelpButton")
                continue;
            foreach (var t in b.GetComponentsInChildren<Text>(true))
                t.color = Color.white;
            foreach (var t in b.GetComponentsInChildren<TMP_Text>(true))
                t.color = Color.white;
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log(
            $"[ArtRevamp] Game UI reskinned — {skinned} buttons. Test Pause/Help in Play mode."
        );
    }

    // Drop a collectible star into the CURRENTLY OPEN scene. It's a small rig: a bobbing star sprite +
    // a gentle gold glow (so it doesn't read as dark), a subtle sparkle particle system (much lighter
    // than the win-tile burst), and a soft ground shadow. Grabbing it in play earns the 3rd star.
    // Re-running REBUILDS an existing 'CollectStar' in place (keeps its position), so it's safe to
    // move the star, save, then re-run to refresh the effects — then drag it onto the tile you want.
    [MenuItem("Tools/Soulmates/Add Collectible Star (open scene)")]
    public static void AddCollectibleStar()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // Preserve placement on re-run; otherwise pick a central tile as a starting point.
        var existing = GameObject.Find("CollectStar");
        Vector3 pos;
        Transform parent;
        if (existing != null)
        {
            pos = existing.transform.position;
            parent = existing.transform.parent;
            Object.DestroyImmediate(existing);
        }
        else
        {
            pos = DefaultStarTile(out parent);
        }

        var starSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Sprites/UI/star_3d.png"
        );
        var sparkleMat = EnsureSparkleMaterial();
        var shadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowSpritePath);
        var shadowMaterial = EnsureShadowMaterial();

        // Root = UNSCALED bobber (StarPickup moves it), so the child light/particles read in world
        // units instead of being scaled by the sprite's shrink factor.
        var root = new GameObject("CollectStar", typeof(StarPickup));
        root.transform.position = pos;
        if (parent != null)
            root.transform.SetParent(parent, true);

        // Visible star
        var starGo = new GameObject("Star", typeof(SpriteRenderer));
        starGo.transform.SetParent(root.transform, false);
        var sr = starGo.GetComponent<SpriteRenderer>();
        sr.sprite = starSprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 500;
        float srcH = starSprite != null ? starSprite.bounds.size.y : 0f;
        starGo.transform.localScale = Vector3.one * (srcH > 0.001f ? 0.44f / srcH : 0.18f); // ~0.44u, a bit smaller
        starGo.transform.localPosition = new Vector3(0f, StarHover, 0f); // float above the tile top
        root.GetComponent<StarPickup>().visual = starGo.transform; // bob ONLY the sprite → shadow stays put

        // Gentle gold glow — fixes the "too dark" look; breathes so it feels alive.
        var glowGo = new GameObject("StarGlow", typeof(Light2D));
        glowGo.transform.SetParent(root.transform, false);
        glowGo.transform.localPosition = new Vector3(0f, StarHover, 0f); // at the star
        var l = glowGo.GetComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.color = new Color(1f, 0.86f, 0.5f);
        l.intensity = 0.45f; // soft — was washing out the tile
        l.pointLightInnerRadius = 0.1f;
        l.pointLightOuterRadius = 1.1f;
        l.blendStyleIndex = 0;
        var lc = glowGo.AddComponent<PlayerLightController>();
        lc.minIntensity = 0.32f;
        lc.maxIntensity = 0.55f;
        lc.breathingSpeed = 2f;
        lc.minRadius = 0.95f;
        lc.maxRadius = 1.2f;
        lc.useRandomOffset = true;

        // Subtle sparkles — a slow trickle of twinkles, NOT the win-tile's dense burst.
        var psGo = new GameObject("Sparkles", typeof(ParticleSystem));
        psGo.transform.SetParent(root.transform, false);
        psGo.transform.localPosition = new Vector3(0f, StarHover, 0f); // at the star
        var ps = psGo.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.6f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 24;
        var em = ps.emission;
        em.rateOverTime = 5f; // gentle
        var shp = ps.shape;
        shp.shapeType = ParticleSystemShapeType.Circle;
        shp.radius = 0.16f;
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.y = new ParticleSystem.MinMaxCurve(0.12f, 0.3f); // drift up
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.3f),
                new GradientAlphaKey(0f, 1f),
            }
        );
        col.color = grad;
        var psr = psGo.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sharedMaterial = sparkleMat;
        psr.sortingLayerName = "Default";
        psr.sortingOrder = 501;
        ps.Play();

        // Soft ground shadow (child so it follows drags; the bob is tiny so it barely moves).
        if (shadowSprite != null)
        {
            var shGo = new GameObject("StarShadow", typeof(SpriteRenderer));
            shGo.transform.SetParent(root.transform, false);
            shGo.transform.localPosition = Vector3.zero; // on the tile top (root sits at the surface)
            shGo.transform.localScale = Vector3.one * 0.42f; // match the smaller star
            var shr = shGo.GetComponent<SpriteRenderer>();
            shr.sprite = shadowSprite;
            if (shadowMaterial != null)
                shr.sharedMaterial = shadowMaterial;
            shr.color = new Color(0f, 0f, 0f, 0.32f);
            shr.sortingLayerName = "Default";
            shr.sortingOrder = 499; // just under the star
        }

        // Sit it on the nearest tile's centre-top, like the slimes (keeps its spot, fixes alignment).
        SnapStarToTile(root.transform, pos);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
        SceneView.FrameLastActiveSceneView(); // snap the Scene view to it so it's obvious
        Debug.Log(
            $"[ArtRevamp] Built 'CollectStar' (glow + sparkles + shadow) at {root.transform.position} in '{scene.name}'. SELECTED — drag it onto the tile you want, then run Snap (or save)."
        );
    }

    // The slimes live at local y = 0.25 above their tile (Player prefab base) — the "on-tile" height.
    const float TileTopLift = 0.12f; // OVERALL height of the whole floating rig on the tile
    const float StarHover = 0.22f; // FLOAT GAP: how high the star hovers above its shadow

    // Snap a star ROOT to the centre of the ground tile nearest `nearWorld`, lifted onto the tile top
    // so it sits like the slimes. Idempotent — re-running won't drift it.
    static bool SnapStarToTile(Transform starRoot, Vector3 nearWorld)
    {
        var tmGo = GameObject.FindWithTag("GroundTileMap");
        if (tmGo == null || !tmGo.TryGetComponent(out UnityEngine.Tilemaps.Tilemap tm))
            return false;
        Vector3Int cell = tm.WorldToCell(nearWorld);
        if (!tm.HasTile(cell))
        {
            float best = float.MaxValue;
            foreach (var c in tm.cellBounds.allPositionsWithin)
            {
                if (!tm.HasTile(c))
                    continue;
                float d = (tm.GetCellCenterWorld(c) - nearWorld).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    cell = c;
                }
            }
        }
        starRoot.position = tm.GetCellCenterWorld(cell) + Vector3.up * TileTopLift;
        return true;
    }

    // Snap 'CollectStar' to the centre-top of the ground tile it's nearest to — reliable on the
    // isometric grid, and it lifts the star onto the tile top like the slimes. Drag the star roughly
    // over the target tile, run this, done.
    [MenuItem("Tools/Soulmates/Snap Collectible Star to Nearest Tile")]
    public static void SnapCollectibleStar()
    {
        var go = GameObject.Find("CollectStar");
        if (go == null)
        {
            Debug.LogWarning("[ArtRevamp] No 'CollectStar' in the open scene.");
            return;
        }
        Undo.RecordObject(go.transform, "Snap CollectStar");
        if (!SnapStarToTile(go.transform, go.transform.position))
        {
            Debug.LogWarning("[ArtRevamp] No GroundTileMap in the open scene.");
            return;
        }
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log(
            $"[ArtRevamp] Snapped 'CollectStar' onto the tile top → {go.transform.position} (lifted {TileTopLift} like the slimes)."
        );
    }

    static Vector3 DefaultStarTile(out Transform gridParent)
    {
        gridParent = null;
        Vector3 pos = Vector3.zero;
        var tmGo = GameObject.FindWithTag("GroundTileMap");
        if (tmGo != null && tmGo.TryGetComponent(out UnityEngine.Tilemaps.Tilemap tm))
        {
            gridParent = tmGo.transform.parent;
            var b = tm.cellBounds;
            var center = new Vector3Int((b.xMin + b.xMax) / 2, (b.yMin + b.yMax) / 2, 0);
            float bestD = float.MaxValue;
            foreach (var c in b.allPositionsWithin)
            {
                if (!tm.HasTile(c))
                    continue;
                float d = (c - center).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    pos = tm.GetCellCenterWorld(c);
                }
            }
        }
        if (gridParent == null)
        {
            var gGo = GameObject.Find("Grid");
            gridParent = gGo != null ? gGo.transform : null;
        }
        return pos;
    }

    // Import star.wav and register it as a "StarCollect" SFX on the AudioManager prefab (reusing the
    // same SFX mixer group as the other effects). StarPickup plays it by name on collect.
    [MenuItem("Tools/Soulmates/Wire Star Collect Sound")]
    public static void WireStarCollectSound()
    {
        const string dst = "Assets/_Project/Audios/star.wav";
        if (!File.Exists(dst) && File.Exists("for_claude/star.wav"))
        {
            File.Copy("for_claude/star.wav", dst, true);
            AssetDatabase.ImportAsset(dst);
        }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(dst);
        if (clip == null)
        {
            Debug.LogWarning(
                "[ArtRevamp] star.wav not found — put it in for_claude/ or Assets/_Project/Audios/."
            );
            return;
        }

        const string path = "Assets/_Project/Prefabs/Audio Manager.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var am = root.GetComponent<AudioManager>();
        if (am == null)
        {
            Debug.LogWarning("[ArtRevamp] AudioManager component missing on the prefab.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        var list = new List<Sound>(am.sounds ?? new Sound[0]);
        var sfx = list.Find(s => s != null && s.name == "Click"); // an existing SFX → copy its group
        var entry = list.Find(s => s != null && s.name == "StarCollect");
        if (entry == null)
        {
            entry = new Sound { name = "StarCollect" };
            list.Add(entry);
        }
        entry.clip = clip;
        entry.volume = 0.8f;
        entry.pitch = 1f;
        entry.loop = false;
        entry.audioMixerGroup = sfx != null ? sfx.audioMixerGroup : entry.audioMixerGroup;
        am.sounds = list.ToArray();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log(
            "[ArtRevamp] Wired 'StarCollect' SFX on the Audio Manager. StarPickup plays it on collect."
        );
    }

    // Set the TIME-star threshold (seconds to finish and still earn the time star) per level. Writes
    // a per-scene override onto each level's Game Controller. Tweak the table below to taste.
    [MenuItem("Tools/Soulmates/Set Time-Star Pars (all levels)")]
    public static void SetTimeStarPars()
    {
        var pars = new[]
        {
            ("Level01", 20f),
            ("Level02", 25f),
            ("Level03", 30f),
            ("Level04", 35f),
            ("Level05", 40f),
            ("Level06", 45f),
            ("Level07", 50f),
            ("Level08", 55f),
            ("Level09", 60f),
        };

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        string current = EditorSceneManager.GetActiveScene().path;
        int done = 0;
        foreach (var (sc, secs) in pars)
        {
            string path = $"Assets/_Project/Scenes/{sc}.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var gc = Object.FindObjectOfType<GameController>();
            if (gc == null)
            {
                Debug.LogWarning($"[ArtRevamp] No GameController in {sc}.");
                continue;
            }
            Undo.RecordObject(gc, "Set time-star par");
            gc.timeParSeconds = secs;
            EditorUtility.SetDirty(gc);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            done++;
        }
        if (!string.IsNullOrEmpty(current))
            EditorSceneManager.OpenScene(current);
        Debug.Log($"[ArtRevamp] Time-star pars set on {done}/9 levels.");
    }

    // Unlit material carrying the sparkle twinkle texture, for the collectible star's particle system.
    static Material EnsureSparkleMaterial()
    {
        const string p = "Assets/_Project/Sprites/SparkleParticle.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            m = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(m, p);
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(UIDir + "sparkle.png");
        if (tex != null)
            m.mainTexture = tex;
        m.color = Color.white;
        AssetDatabase.SaveAssets();
        return m;
    }

    const string CharDir = "Assets/_Project/Sprites/Characters/";

    // Swap the old blob players for the new slimes (Grid prefab) + Level Complete images,
    // wire front/back facing, and recolor the goal tiles to match.
    [MenuItem("Tools/Soulmates/Apply Characters")]
    public static void ApplyCharacters()
    {
        // blink frames are optional — configured only if you've generated them
        foreach (
            var f in new[]
            {
                "char_boy_front",
                "char_boy_back",
                "char_boy_blink",
                "char_girl_front",
                "char_girl_back",
                "char_girl_blink",
            }
        )
            if (System.IO.File.Exists(CharDir + f + ".png"))
                ConfigureCharSprite(CharDir + f + ".png");

        if (System.IO.File.Exists(ShadowSpritePath))
            ConfigureWorldSprite(ShadowSpritePath); // centre pivot, 1 unit wide → CharacterShadow scales it
        shadowMat = EnsureShadowMaterial(); // before the Grid prefab opens

        var boyFront = LoadChar("char_boy_front");
        var boyBack = LoadChar("char_boy_back");
        var boyBlink = LoadChar("char_boy_blink"); // null until generated
        var girlFront = LoadChar("char_girl_front");
        var girlBack = LoadChar("char_girl_back");
        var girlBlink = LoadChar("char_girl_blink");

        // Grid prefab: players + goal tiles (applies to every level)
        const string gridPath = "Assets/_Project/Prefabs/Grid.prefab";
        var grid = PrefabUtility.LoadPrefabContents(gridPath);
        var boyLight = new Color(0.45f, 0.72f, 1f); // blue glow
        var girlLight = new Color(1f, 0.55f, 0.74f); // pink glow
        SetupPlayer(FindIn(grid.transform, "Player1"), boyFront, boyBack, boyBlink, boyLight);
        SetupPlayer(FindIn(grid.transform, "Player2"), girlFront, girlBack, girlBlink, girlLight);
        TintTile(FindIn(grid.transform, "WinTile1"), new Color(0.56f, 0.77f, 1f)); // blue (boy)
        TintTile(FindIn(grid.transform, "WinTile2"), new Color(1f, 0.70f, 0.82f)); // pink (girl)
        PrefabUtility.SaveAsPrefabAsset(grid, gridPath);
        PrefabUtility.UnloadPrefabContents(grid);

        // Level Complete character images
        const string gcPath = "Assets/_Project/Prefabs/Game Canvas.prefab";
        var gc = PrefabUtility.LoadPrefabContents(gcPath);
        SetImageSprite(FindIn(gc.transform, "Player1Image"), boyFront);
        SetImageSprite(FindIn(gc.transform, "Player2Image"), girlFront);
        PrefabUtility.SaveAsPrefabAsset(gc, gcPath);
        PrefabUtility.UnloadPrefabContents(gc);

        Debug.Log("[ArtRevamp] Characters applied. Play a level to test movement, facing, hop.");
    }

    static void ConfigureCharSprite(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti))
        {
            Debug.LogWarning("[ArtRevamp] char sprite not found: " + path);
            return;
        }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        // player scale is 0.1, so a low PPU is needed for tile-sized slimes
        settings.spritePixelsPerUnit = 132f;
        // pivot near the slime's base so its bottom lands on the cell center (grounded)
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(0.5f, 0.3f);
        ti.SetTextureSettings(settings);
        ti.SaveAndReimport();
    }

    static Sprite LoadChar(string name) =>
        AssetDatabase.LoadAssetAtPath<Sprite>(CharDir + name + ".png");

    const float HugScale = 0.16f; // world scale of the hug couple on the tile — bigger so it reads as a pair
    const float HugLift = 0.24f; // raise the couple so it sits ON the tile centre (not sunk low)

    // Show a single hugging-couple sprite when the two slimes meet on one tile (replaces the old
    // two stacked clones). Imports char_hug(+_back), adds a "Hug" child to the Player1+Player2 set,
    // and wires GameController.hugFront/hugBack. Front/back is chosen at runtime from facing.
    [MenuItem("Tools/Soulmates/Apply Hug Stack")]
    public static void ApplyHugStack()
    {
        foreach (var f in new[] { "char_hug", "char_hug_back" })
            if (System.IO.File.Exists(CharDir + f + ".png"))
                ConfigureHugSprite(CharDir + f + ".png");
        shadowMat = EnsureShadowMaterial(); // before the Grid prefab opens

        var front = LoadChar("char_hug");
        var back = LoadChar("char_hug_back");
        if (front == null)
        {
            Debug.LogWarning(
                "[ArtRevamp] char_hug.png not found in Characters/ — hug not applied."
            );
            return;
        }

        // 1) Grid prefab: add the "Hug" sprite as a child of the Player1+Player2 stack.
        const string gridPath = "Assets/_Project/Prefabs/Grid.prefab";
        var grid = PrefabUtility.LoadPrefabContents(gridPath);
        var set = FindIn(grid.transform, "Player1+Player2");
        if (set != null)
        {
            // Permanently hide the two old blob clones — we use the hug now, and they were showing
            // in the Scene view at edit time (the set stays active so it can be found at runtime).
            foreach (var cloneName in new[] { "Player1Clone", "Player2Clone" })
            {
                var c = FindIn(set, cloneName);
                if (c != null)
                    c.gameObject.SetActive(false);
            }
            // fall back to child index if the clones aren't named as expected
            for (int i = 0; i < 2 && i < set.childCount; i++)
            {
                var child = set.GetChild(i);
                if (child.name != "Hug" && child.name != "Heart")
                    child.gameObject.SetActive(false);
            }

            var hugT = set.Find("Hug");
            var hug =
                hugT != null ? hugT.gameObject : new GameObject("Hug", typeof(SpriteRenderer));
            if (hugT == null)
                hug.transform.SetParent(set, false);
            var sr = hug.GetComponent<SpriteRenderer>();
            sr.sprite = front;
            sr.sortingOrder = 20; // above the tiles
            hug.transform.localPosition = new Vector3(0f, HugLift, 0f); // lifted onto the tile centre
            hug.transform.localScale = Vector3.one * HugScale;

            // idle breathing + entrance pop (so the couple feels alive like the slimes)
            var life = hug.GetComponent<HugLife>() ?? hug.AddComponent<HugLife>();
            life.bobAmount = 0.05f; // a bit more visible than the default
            life.bobSpeed = 2.6f;
            life.squash = 0.06f;

            // soft warm glow around the couple (like the slimes' breathing light)
            var glowT = hug.transform.Find("Glow");
            var glow = glowT != null ? glowT.gameObject : new GameObject("Glow");
            if (glowT == null)
                glow.transform.SetParent(hug.transform, false);
            glow.transform.localPosition = Vector3.zero;
            var light = glow.GetComponent<Light2D>() ?? glow.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1f, 0.7f, 0.8f); // soft pink
            light.intensity = 0.45f; // gentle — was too bright on the stacked couple
            light.pointLightInnerRadius = 0.3f;
            light.pointLightOuterRadius = 3.5f; // ×HugScale ≈ 0.4 world units of glow
            light.blendStyleIndex = 0;
            var lso = new SerializedObject(light);
            var layers = lso.FindProperty("m_ApplyToSortingLayers");
            if (layers != null)
            {
                layers.arraySize = 1;
                layers.GetArrayElementAtIndex(0).intValue = 0; // "Default" layer
                lso.ApplyModifiedProperties();
            }

            // Ground shadow for the couple. The couple HOP is driven by the moving slime's transform
            // (the set follows it) while the hug's localPosition stays put — so the shadow must read the
            // two slimes' currentLift (+ the hug's idle bob), else it floats up with them on a jump.
            var p1c = FindIn(grid.transform, "Player1")?.GetComponent<PlayerController>();
            var p2c = FindIn(grid.transform, "Player2")?.GetComponent<PlayerController>();
            AttachShadow(hug.transform, sr, p1c, "HugShadow", 0.18f, 0.55f, 0.5f, p2c, true);

            hug.SetActive(false); // GameController switches it on (and picks front/back) when stacked
        }
        else
        {
            Debug.LogWarning("[ArtRevamp] Player1+Player2 set not found in Grid prefab.");
        }
        PrefabUtility.SaveAsPrefabAsset(grid, gridPath);
        PrefabUtility.UnloadPrefabContents(grid);

        // 2) Game Controller prefab: assign the hugFront / hugBack sprite references.
        const string gcPath = "Assets/_Project/Prefabs/Game Controller.prefab";
        var gc = PrefabUtility.LoadPrefabContents(gcPath);
        var ctrl = gc.GetComponent<GameController>();
        if (ctrl != null)
        {
            var so = new SerializedObject(ctrl);
            so.FindProperty("hugFront").objectReferenceValue = front;
            so.FindProperty("hugBack").objectReferenceValue = back;
            so.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogWarning(
                "[ArtRevamp] GameController not found on Game Controller prefab root."
            );
        }
        PrefabUtility.SaveAsPrefabAsset(gc, gcPath);
        PrefabUtility.UnloadPrefabContents(gc);

        Debug.Log("[ArtRevamp] Hug stack applied. Move both slimes onto one tile to test.");
    }

    static void ConfigureHugSprite(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti))
            return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spritePixelsPerUnit = 132f; // same as the slimes
        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spritePivot = new Vector2(0.5f, 0.35f); // sit grounded on the tile
        ti.SetTextureSettings(s);
        ti.SaveAndReimport();
    }

    static void SetupPlayer(
        Transform player,
        Sprite front,
        Sprite back,
        Sprite blink,
        Color lightColor
    )
    {
        if (player == null)
        {
            Debug.LogWarning("[ArtRevamp] player not found in Grid prefab.");
            return;
        }
        if (player.TryGetComponent(out SpriteRenderer sr))
            sr.sprite = front;
        if (player.TryGetComponent(out Animator anim))
            anim.enabled = false; // stop the blob frame animation
        if (player.TryGetComponent(out PlayerController pc))
        {
            pc.frontSprite = front;
            pc.backSprite = back;
            pc.blinkSprite = blink;
        }
        // recolor the character's breathing glow to match
        var lightT = FindIn(player, "Sprite Light 2D");
        if (lightT != null && lightT.TryGetComponent(out Light2D light))
            light.color = lightColor;

        if (player.TryGetComponent(out SpriteRenderer psr))
            AttachShadow(player, psr, pc, player.name + "Shadow");
    }

    const string ShadowSpritePath = "Assets/_Project/Sprites/Characters/shadow_soft.png";
    static Material shadowMat; // unlit material for the ground shadows (set before a prefab opens)

    // Create/load the unlit shadow material. Call BEFORE LoadPrefabContents (AssetDatabase writes while
    // a prefab is open have bitten us before).
    static Material EnsureShadowMaterial()
    {
        const string p = "Assets/_Project/Sprites/ShadowUnlit.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            m = new Material(Shader.Find("Sprites/Default")); // unlit transparent in URP
            AssetDatabase.CreateAsset(m, p);
            AssetDatabase.SaveAssets();
        }
        return m;
    }

    // Add (or refresh) a soft ground shadow as a SIBLING of the character — never a child, so it can't
    // inherit hop-squash or a back-flip spin. CharacterShadow keeps it planted on the floor. Used for
    // both the slimes (liftSource = their PlayerController) and the hug couple (liftSource = null).
    static void AttachShadow(
        Transform follow,
        SpriteRenderer followSR,
        PlayerController liftSource,
        string shName,
        float footFactor = 0.45f,
        float widthFactor = 0.95f,
        float baseAlpha = 0.5f,
        PlayerController liftSourceB = null,
        bool useLocalBob = false
    )
    {
        var shadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowSpritePath);
        if (shadowSprite == null)
        {
            Debug.LogWarning("[ArtRevamp] shadow sprite missing: " + ShadowSpritePath);
            return;
        }
        var parent = follow.parent != null ? follow.parent : follow;

        Transform st = null;
        foreach (Transform c in parent)
            if (c.name == shName)
            {
                st = c;
                break;
            }
        GameObject sgo =
            st != null
                ? st.gameObject
                : new GameObject(shName, typeof(SpriteRenderer), typeof(CharacterShadow));
        if (st == null)
            sgo.transform.SetParent(parent, false);

        var sr = sgo.GetComponent<SpriteRenderer>();
        sr.sprite = shadowSprite;
        sr.color = new Color(0f, 0f, 0f, 0.42f);
        // UNLIT material — otherwise the slime's breathing glow lights up the dark shadow and washes
        // it out, so the slime reads as floating. Sprites/Default is unlit in this URP setup.
        if (shadowMat != null)
            sr.sharedMaterial = shadowMat;
        if (followSR != null)
        {
            sr.sortingLayerID = followSR.sortingLayerID;
            sr.sortingOrder = followSR.sortingOrder - 1;
        }
        var cs = sgo.GetComponent<CharacterShadow>() ?? sgo.AddComponent<CharacterShadow>();
        cs.liftSource = liftSource;
        cs.liftSourceB = liftSourceB;
        cs.useLocalBob = useLocalBob;
        cs.follow = follow;
        cs.followRenderer = followSR;
        // Set the tunables EXPLICITLY — re-running must overwrite an existing component's old values
        // (otherwise changing the script defaults does nothing to already-serialized shadows).
        cs.footFactor = footFactor; // plant near the feet (0 = pivot, 1 = texture bottom)
        cs.widthFactor = widthFactor;
        cs.baseAlpha = baseAlpha;
        EditorUtility.SetDirty(cs);
        sgo.transform.position = follow.position; // start under the character
    }

    static void TintTile(Transform tile, Color c)
    {
        if (tile != null && tile.TryGetComponent(out SpriteRenderer sr))
            sr.color = c;
    }

    static void SetImageSprite(Transform t, Sprite sprite)
    {
        if (t != null && t.TryGetComponent(out Image img))
            img.sprite = sprite;
    }

    // Shared kawaii card: pink rounded frame + cream inner + heart accents. Anchored to the
    // parent by screen fractions. Used by pause, help (and matches the options card).
    static GameObject AddCard(Transform parent, string name, Vector2 aMin, Vector2 aMax)
    {
        var panel = CardSprite();
        RemoveChild(parent.gameObject, name);
        RemoveChild(parent.gameObject, name + "Shadow");

        // Soft drop-shadow FIRST (the panel silhouette, nudged down) so the card floats.
        var shadow = new GameObject(
            name + "Shadow",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        shadow.transform.SetParent(parent, false);
        shadow.transform.SetAsFirstSibling();
        var sh = shadow.GetComponent<Image>();
        sh.sprite = panel;
        sh.type = Image.Type.Sliced;
        sh.raycastTarget = false;
        sh.color = CardShadowSoft;
        var srt = shadow.GetComponent<RectTransform>();
        srt.anchorMin = aMin;
        srt.anchorMax = aMax;
        srt.offsetMin = new Vector2(-3f, -14f); // cast down + a touch out
        srt.offsetMax = new Vector2(3f, -4f);

        // The DESIGNED modern card sprite (ui_card) IS the panel — clean cream, soft edge.
        var card = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        card.transform.SetParent(parent, false);
        card.transform.SetSiblingIndex(shadow.transform.GetSiblingIndex() + 1); // just above its shadow
        var f = card.GetComponent<Image>();
        f.sprite = panel;
        f.type = Image.Type.Sliced;
        f.raycastTarget = false;
        f.color = Color.white; // the sprite IS the design — no tint
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = aMin;
        crt.anchorMax = aMax;
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;
        return card;
    }

    // Build the Help screen: title + two character control diagrams + objective + Back.
    static void BuildHelpContent(GameObject help)
    {
        var rose = TextPlum; // kit heading/text colour — consistent across menu, options, pause, help, win
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/_Project/Fonts/Roboto-Medium SDF.asset"
        );

        var card = AddCard(
            help.transform,
            "HelpCard",
            new Vector2(0.18f, 0.1f),
            new Vector2(0.82f, 0.92f)
        );

        AddText(
            card.transform,
            "Title",
            "How to Play",
            font,
            rose,
            40f,
            FontStyles.Bold,
            new Vector2(0.06f, 0.82f),
            new Vector2(0.94f, 0.90f), // pulled down from the card's top edge for breathing room
            TextAlignmentOptions.Center
        );

        BuildControl(
            card.transform,
            font,
            rose,
            0.3f,
            LoadChar("char_boy_front"),
            "Player 1",
            new[] { "W", "A", "S", "D" }
        );
        BuildControl(
            card.transform,
            font,
            rose,
            0.7f,
            LoadChar("char_girl_front"),
            "Player 2",
            new[] { "↑", "←", "↓", "→" }
        );

        AddText(
            card.transform,
            "Obj",
            "Get both soulmates onto their glowing tiles — together! ♥",
            font,
            rose,
            22f,
            FontStyles.Normal,
            new Vector2(0.08f, 0.24f),
            new Vector2(0.92f, 0.31f),
            TextAlignmentOptions.Center
        );

        // Back button — recreated fresh each run as a child of the HelpScreen (NOT the card, which is
        // rebuilt and would delete it). Anchored screen-relative inside the card (card spans y
        // 0.10–0.92), just above its bottom edge, below the objective text.
        RemoveChild(help, "BackButton");
        var back = new GameObject(
            "BackButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );
        back.transform.SetParent(help.transform, false);
        var brt = back.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.215f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(150f, 56f); // slimmer, closer to the other in-game buttons
        brt.anchoredPosition = Vector2.zero;

        var backLabel = new GameObject("Text", typeof(RectTransform));
        backLabel.transform.SetParent(back.transform, false);
        var backTmp = backLabel.AddComponent<TextMeshProUGUI>();
        if (font != null)
            backTmp.font = font;
        backTmp.text = "Close";
        backTmp.alignment = TextAlignmentOptions.Center;
        backTmp.enableAutoSizing = true;
        backTmp.fontSizeMin = 8f;
        backTmp.fontSizeMax = 28f;
        var blrt = backLabel.GetComponent<RectTransform>();
        blrt.anchorMin = Vector2.zero;
        blrt.anchorMax = Vector2.one;
        blrt.offsetMin = Vector2.zero;
        blrt.offsetMax = Vector2.zero;

        back.transform.SetAsLastSibling();
        SkinButton(back, Load("ui_button.png"), false);
    }

    // One player's diagram: the character in the middle with its 4 movement keys around it,
    // arranged in the isometric directions as diamond "tiles" (Z-tilt + 2:1 Y-compression).
    static void BuildControl(
        Transform card,
        TMP_FontAsset font,
        Color rose,
        float colX,
        Sprite charSprite,
        string label,
        string[] keys
    )
    {
        var pad = new GameObject("Pad", typeof(RectTransform));
        pad.transform.SetParent(card, false);
        var prt = pad.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(colX, 0.62f); // raised so blocks clear the labels
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(230f, 190f);
        prt.localScale = Vector3.one * 0.9f; // shrink the diagram a touch to open up vertical space

        // The 4 keys live on a tilted plane (rotated on Z) so the whole grid leans like the
        // isometric ground; the character sits upright on top of it.
        var plane = new GameObject("KeyPlane", typeof(RectTransform));
        plane.transform.SetParent(pad.transform, false);
        var plrt = plane.GetComponent<RectTransform>();
        plrt.anchorMin = plrt.anchorMax = new Vector2(0.5f, 0.5f);
        plrt.pivot = new Vector2(0.5f, 0.5f);
        plrt.sizeDelta = new Vector2(230f, 190f);
        plrt.localEulerAngles = Vector3.zero; // no lean — the real iso grass blocks carry the perspective

        // up→upper-left, right→upper-right, left→lower-left, down→lower-right
        float hx = 62f,
            vy = 31f; // 2:1 compression = isometric foreshortening (pulled in toward the slime)

        // real in-game grass blocks under the slime + each key, tessellated like the gameplay grid
        AddGround(plane.transform, hx, vy);
        AddKeyCap(plane.transform, new Vector2(-hx, vy), keys[0], font); // up
        AddKeyCap(plane.transform, new Vector2(hx, vy), keys[3], font); // right
        AddKeyCap(plane.transform, new Vector2(-hx, -vy), keys[1], font); // left
        AddKeyCap(plane.transform, new Vector2(hx, -vy), keys[2], font); // down

        // character in the centre (on top of the keys)
        if (charSprite != null)
        {
            // soft ground shadow first (behind the slime) so it reads grounded like in gameplay
            var shGo = new GameObject(
                "CharShadow",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            shGo.transform.SetParent(pad.transform, false);
            var shImg = shGo.GetComponent<Image>();
            shImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowSpritePath);
            shImg.color = new Color(0.25f, 0.15f, 0.22f, 0.34f); // soft plum-black, clearly visible
            shImg.raycastTarget = false;
            var shrt = shGo.GetComponent<RectTransform>();
            shrt.anchorMin = shrt.anchorMax = new Vector2(0.5f, 0.5f);
            shrt.pivot = new Vector2(0.5f, 0.5f);
            shrt.anchoredPosition = new Vector2(0f, -10f); // on the CENTRE tile, just under the slime's feet
            shrt.sizeDelta = new Vector2(88f, 34f);

            var img = new GameObject(
                "Char",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            img.transform.SetParent(pad.transform, false);
            var ci = img.GetComponent<Image>();
            ci.sprite = charSprite;
            ci.preserveAspect = true;
            ci.raycastTarget = false;
            var rt = img.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // sit on the CENTRE tile, lifted a little so the slime's base rests on it (not sunk low)
            rt.anchoredPosition = new Vector2(0f, 24f);
            rt.sizeDelta = new Vector2(96f, 116f);
        }

        AddText(
            card,
            label,
            label,
            font,
            rose,
            24f,
            FontStyles.Bold,
            new Vector2(colX - 0.16f, 0.32f),
            new Vector2(colX + 0.16f, 0.39f),
            TextAlignmentOptions.Center
        );
    }

    const float KeyHeight = 9f; // top-face lift used to seat the slime on the centre key
    const float KeySeat = 10f; // lift the cap UP so it sits centred on the grass tile's top face

    static void AddKeyCap(Transform parent, Vector2 localPos, string label, TMP_FontAsset font)
    {
        var tile = new GameObject("Key", typeof(RectTransform));
        tile.transform.SetParent(parent, false);
        var ktr = tile.GetComponent<RectTransform>();
        ktr.anchorMin = ktr.anchorMax = new Vector2(0.5f, 0.5f);
        ktr.pivot = new Vector2(0.5f, 0.5f);
        ktr.anchoredPosition = localPos + new Vector2(0f, KeySeat);
        ktr.sizeDelta = Vector2.zero;

        // A single BAKED 3D candy key: lit top face + a real shaded extruded side wall (key_cap.png).
        // Its top-face centre sits at the sprite's centre, so the legend can be placed at (0,0).
        var capSprite = Load("key_cap.png");
        var cap = new GameObject(
            "Cap",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        cap.transform.SetParent(tile.transform, false);
        var capImg = cap.GetComponent<Image>();
        capImg.sprite = capSprite;
        capImg.raycastTarget = false;
        capImg.preserveAspect = true;
        var crt = cap.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        // sprite is 132×122; bigger, chunkier keys
        crt.sizeDelta = new Vector2(82f, 82f * 122f / 132f);

        // Legend — a PRINTED key marking, not flat text. Arrow keys get a crisp chevron ICON;
        // letters stay as bold glyphs. Both sit on a soft drop-shadow so they read as moulded into
        // the candy cap, and both are dead-centred (the icon route fixes the off-centre arrows).
        var plum = TextPlum;
        var legendShadow = new Color(0.55f, 0.30f, 0.42f, 0.5f); // darker plum, nudged down = depth

        // up-pointing chevron sprite rotated to the key's direction (null ⇒ it's a letter, use text)
        float? arrowZ =
            label == "↑" ? 0f
            : label == "→" ? -90f
            : label == "↓" ? 180f
            : label == "←" ? 90f
            : (float?)null;

        if (arrowZ.HasValue)
        {
            var arrow = Load("key_arrow.png");
            void ArrowLayer(string n, Vector2 off, Color col)
            {
                var g = new GameObject(
                    n,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );
                g.transform.SetParent(tile.transform, false);
                var im = g.GetComponent<Image>();
                im.sprite = arrow;
                im.color = col;
                im.raycastTarget = false;
                im.preserveAspect = true;
                var rt = g.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(26f, 26f);
                rt.anchoredPosition = off;
                rt.localEulerAngles = new Vector3(0f, 0f, arrowZ.Value);
                g.transform.SetAsLastSibling();
            }
            ArrowLayer("kShadow", new Vector2(0f, -2f), legendShadow);
            ArrowLayer("k", Vector2.zero, plum);
        }
        else
        {
            void TextLayer(string n, Vector2 off, Color col)
            {
                var g = new GameObject(n, typeof(RectTransform));
                g.transform.SetParent(tile.transform, false);
                var t = g.AddComponent<TextMeshProUGUI>();
                if (font != null)
                    t.font = font;
                t.text = label;
                t.color = col;
                t.fontStyle = FontStyles.Bold;
                t.alignment = TextAlignmentOptions.Center;
                t.raycastTarget = false;
                t.enableAutoSizing = false;
                t.fontSize = 24f; // fixed (was autosizing up to 30 → looked oversized)
                var rt = g.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(40f, 40f);
                rt.anchoredPosition = off;
                g.transform.SetAsLastSibling();
            }
            TextLayer("kShadow", new Vector2(0f, -2f), legendShadow);
            TextLayer("k", Vector2.zero, plum);
        }
    }

    // Lay the real in-game grass block under the character (centre) and under each of the 4 keys, in
    // iso so the blocks tessellate like the gameplay grid. Tile-top width = key spacing; drawn
    // back-to-front so each block's dirt side overlaps the one behind it correctly.
    static void AddGround(Transform parent, float hx, float vy)
    {
        var grass = LoadGrassTile();
        if (grass == null)
            return;
        float w = 2f * hx; // top-diamond width spans one cell, so neighbours meet edge-to-edge
        float h = w * (grass.rect.height / grass.rect.width); // keep the block's own proportions
        // The block's TOP-diamond centre sits above the sprite centre (dirt hangs below). For a 2:1
        // top (height = w/2) plus the dirt, that gap is h/2 - w/4. Shift each block down by it so the
        // top-diamond centre lands exactly on the cell point — which is what makes them tessellate.
        float topOffset = h / 2f - w / 4f;

        // back (top row) first, then centre, then front (bottom row)
        var spots = new[]
        {
            new Vector2(-hx, vy),
            new Vector2(hx, vy),
            new Vector2(0f, 0f),
            new Vector2(-hx, -vy),
            new Vector2(hx, -vy),
        };
        foreach (var p in spots)
        {
            var go = new GameObject(
                "Ground",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = grass;
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(p.x, p.y - topOffset);
            rt.sizeDelta = new Vector2(w, h);
        }
    }

    const string GroundSheet = "Assets/_Project/Sprites/Grass-Spritesheet_Blocks.png";
    const string GroundTileName = "Grass-Spritesheet_Blocks_6"; // sheet fallback for the Help diagram
    const string NewGroundTile = "Assets/_Project/Sprites/tile_grass_1.png";

    // Two things give clean tessellation WITHOUT disturbing the slimes/fall:
    //  1. art's TOP FACE is a true 2:1 diamond — the tile_grass_*.png were squashed vertically x0.793
    //     to 726x505 (a too-tall top opens gaps on the diagonal seams). One-off PIL step, not here —
    //     keep the PNGs at 2:1.
    //  2. top-face spans exactly one cell wide -> PPU = width / 1.0.
    // Keep the ORIGINAL convention otherwise: CENTRE pivot + the tilemap's hand-tuned
    // TileAnchor {0.1, 0, 0.5} (do NOT touch the anchor — its z=0.5 must match GetCellCenterWorld or
    // the tile-fall effect jumps; its x/y keep the slimes centred on tiles). On the squashed tile a
    // centre pivot lands the top-face within ~0.005u of where the original tile sat, so nothing shifts.
    const float GroundTileWorldWidth = 1.05f; // ~5% over one cell → tiles overlap a hair so seams (hairline

    // gaps between exact-fit soft-edged tiles) are hidden; iso sorting covers the overlap. Was 1.0 (clean
    // squash) but soft-edged tiles still showed thin seams.

    // Load the ground block for the HELP diagram — the new generated tile if it exists, else the
    // original sheet block, so the diagram matches the levels.
    static Sprite LoadGrassTile()
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(NewGroundTile);
        if (s != null)
            return s;
        foreach (var a in AssetDatabase.LoadAllAssetRepresentationsAtPath(GroundSheet))
            if (a is Sprite gs && gs.name == GroundTileName)
                return gs;
        Debug.LogWarning("[ArtRevamp] no ground tile found.");
        return null;
    }

    // Replace the gameplay ground with the new generated tile across ALL levels: import tile_grass.png
    // at the original tile's world size (413px @ PPU100 ≈ 4.13 units) + centre pivot, then point every
    // painted grass Tile asset at it. No repainting — the tilemap just renders the new sprite. Revert
    // any time with `git checkout Assets/_Project/Tiles/`.
    [MenuItem("Tools/Soulmates/Apply Ground Tile")]
    public static void ApplyGroundTile()
    {
        // collect whichever variants exist (1 = uniform, 2-3 = spread across the painted tiles)
        var variants = new System.Collections.Generic.List<Sprite>();
        foreach (
            var p in new[]
            {
                "Assets/_Project/Sprites/tile_grass_1.png",
                "Assets/_Project/Sprites/tile_grass_2.png",
                "Assets/_Project/Sprites/tile_grass_3.png",
                "Assets/_Project/Sprites/tile_grass_4.png",
                "Assets/_Project/Sprites/tile_grass_5.png",
                "Assets/_Project/Sprites/tile_grass_6.png",
            }
        )
        {
            var s = ImportGroundSprite(p);
            if (s != null)
                variants.Add(s);
        }
        if (variants.Count == 0)
        {
            Debug.LogWarning(
                "[ArtRevamp] No tile_grass*.png in Sprites/ — drop at least tile_grass.png in first."
            );
            return;
        }

        int i = 0,
            n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Tile", new[] { "Assets/_Project/Tiles" }))
        {
            var tp = AssetDatabase.GUIDToAssetPath(guid);
            if (!System.IO.Path.GetFileName(tp).StartsWith("Grass-Spritesheet_Blocks"))
                continue; // only the ground tiles
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(tp) is { } t)
            {
                t.sprite = variants[i % variants.Count]; // round-robin the variants for variety
                i++;
                EditorUtility.SetDirty(t);
                n++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"[ArtRevamp] {variants.Count} ground variant(s) spread across {n} grass tiles (all levels). Reopen a level."
        );
    }

    // Import a ground tile PNG at the original tile's world size (413px @ PPU100 ≈ 4.13u) + centre
    // pivot, so it drops into the iso grid. Returns the sprite, or null if the file isn't there.
    static Sprite ImportGroundSprite(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti))
            return null;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Center; // original convention; pairs with the TileAnchor
        // GroundTileWorldWidth = how many world units the tile's full width spans (1.0 = one cell).
        s.spritePixelsPerUnit = tex != null ? tex.width / GroundTileWorldWidth : 100f;
        ti.SetTextureSettings(s);
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // A Tilemap bakes its tiles' SPRITES into the scene when saved. Repointing the Tile assets (via
    // Apply Ground Tile) updates the assets but NOT the already-saved scenes — they keep showing the
    // OLD sprites until a refresh event (e.g. switching scenes) fires, and a BUILD ships the stale
    // baked sprites. This opens every scene, forces the tilemaps to re-pull their tiles' current
    // sprites, and re-saves — so all scenes (and builds) show the current ground tile. Run after
    // Apply Ground Tile. It will prompt to save any unsaved work first.
    [MenuItem("Tools/Soulmates/Rebake Ground Tiles In All Scenes")]
    public static void RebakeGroundTilesInAllScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return; // user cancelled
        string current = EditorSceneManager.GetActiveScene().path;
        int scenes = 0,
            maps = 0;
        foreach (
            var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" })
        )
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool changed = false;
            foreach (var tm in Object.FindObjectsOfType<UnityEngine.Tilemaps.Tilemap>())
            {
                tm.RefreshAllTiles(); // re-pull each tile's CURRENT sprite into the tilemap cache
                changed = true;
                maps++;
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scenes++;
            }
        }
        if (!string.IsNullOrEmpty(current))
            EditorSceneManager.OpenScene(current);
        Debug.Log(
            $"[ArtRevamp] Rebaked {maps} tilemap(s) across {scenes} scene(s). Tiles now persist."
        );
    }

    // Master switch for the random ground-tile mirroring. FALSE = all tiles flat/un-flipped (the menu
    // below actively resets them). Set TRUE to bring the per-cell random flip back.
    const bool FlipGroundTiles = false;

    // Randomly mirror ~half the ground tiles (per-cell horizontal flip) so the grid feels less uniform.
    // Deterministic per cell position → stable across re-runs (same pattern every time). Refreshes the
    // tilemap first (so it has the current sprites) THEN applies the flip, so it's self-contained and
    // order-safe. Run AFTER Apply Ground Tile. Re-run to keep it after any Rebake.
    // Honours FlipGroundTiles: when false, this resets every tile to identity (clears existing flips).
    [MenuItem("Tools/Soulmates/Flip Random Ground Tiles (All Scenes)")]
    public static void FlipRandomGroundTiles()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        string current = EditorSceneManager.GetActiveScene().path;
        var flipped = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(-1f, 1f, 1f));
        int scenes = 0,
            flips = 0;
        foreach (
            var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" })
        )
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool changed = false;
            foreach (var tm in Object.FindObjectsOfType<UnityEngine.Tilemaps.Tilemap>())
            {
                tm.RefreshAllTiles(); // ensure current sprites first (RefreshAllTiles resets per-cell transforms)
                foreach (var cell in tm.cellBounds.allPositionsWithin)
                {
                    if (!tm.HasTile(cell))
                        continue;
                    // deterministic pseudo-random → ~50% flipped, identical every run (only when enabled)
                    int h = (cell.x * 73856093) ^ (cell.y * 19349663);
                    bool flip = FlipGroundTiles && (h & 1) == 0;
                    tm.SetTransformMatrix(cell, flip ? flipped : Matrix4x4.identity);
                    if (flip)
                        flips++;
                    changed = true;
                }
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scenes++;
            }
        }
        if (!string.IsNullOrEmpty(current))
            EditorSceneManager.OpenScene(current);
        Debug.Log(
            FlipGroundTiles
                ? $"[ArtRevamp] Flipped {flips} ground tiles across {scenes} scene(s)."
                : $"[ArtRevamp] Ground-tile flipping OFF — reset all tiles flat across {scenes} scene(s)."
        );
    }

    // Reskin the Level Complete (win) screen to the kawaii theme: drop the red/orange title gradient
    // for clean rose, turn the wood banner into a soft pink panel, and make the buttons consistent.
    // First pass — the prefab has many unnamed objects, so the banner is the title's parent Image.
    [MenuItem("Tools/Soulmates/Apply Win Screen")]
    public static void ApplyWinScreen()
    {
        const string path = "Assets/_Project/Prefabs/Game Canvas.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var lc = FindIn(root.transform, "LevelComplete");
        if (lc == null)
        {
            Debug.LogWarning("[ArtRevamp] 'LevelComplete' not found in Game Canvas prefab.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }
        // ── REBUILT FROM SCRATCH (idempotent) ── dim + cream card + fresh celebratory title, the two
        // slimes side by side, a heart, and a clean 3-button row. Old backing art is hidden; the
        // functional buttons keep their wiring (ExitMenu has the menu hook; Replay/Next → popup methods).
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/_Project/Fonts/Roboto-Medium SDF.asset"
        );
        var popup = FindIn(lc, "Popup") ?? lc;
        const float refW = 800f,
            refH = 600f;
        float cw = refW * 0.64f;
        float ch = refH * 0.86f; // taller to fit the star-reveal row + rays comfortably

        // hide the old backing panels + title (we build fresh ones)
        foreach (var oldName in new[] { "ForestBackground", "Image", "LevelCompleteText" })
        {
            var ot = FindIn(popup, oldName);
            if (ot != null)
                ot.gameObject.SetActive(false);
        }

        var dim = MakeImage(popup, "WinDim", null, new Color(0f, 0f, 0f, 0.5f));
        var dimrt = dim.GetComponent<RectTransform>();
        dimrt.anchorMin = Vector2.zero;
        dimrt.anchorMax = Vector2.one;
        dimrt.offsetMin = Vector2.zero;
        dimrt.offsetMax = Vector2.zero;
        dim.transform.SetAsFirstSibling();

        var cardSprite = CardSprite();
        var wShadow = MakeImage(popup, "WinShadow", cardSprite, CardShadowSoft);
        var wsrt = wShadow.GetComponent<RectTransform>();
        wsrt.anchorMin = wsrt.anchorMax = new Vector2(0.5f, 0.5f);
        wsrt.pivot = new Vector2(0.5f, 0.5f);
        wsrt.sizeDelta = new Vector2(cw + 4f, ch + 4f);
        wsrt.anchoredPosition = new Vector2(0f, -refH * 0.018f);
        wShadow.transform.SetSiblingIndex(1);

        var card = MakeImage(popup, "WinCard", cardSprite, Color.white);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(cw, ch);
        crt.anchoredPosition = Vector2.zero;
        card.transform.SetSiblingIndex(2);

        // ── Sunburst rays behind the slimes (slowly rotating) — the celebratory backdrop ──
        var rayS = Load("ray_burst.png");
        if (rayS != null)
        {
            var rays = MakeImage(popup, "WinRays", rayS, new Color(1f, 0.85f, 0.55f, 0.5f)); // warm gold glow
            var rrt = rays.GetComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
            rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.sizeDelta = new Vector2(ch * 0.7f, ch * 0.7f);
            rrt.anchoredPosition = new Vector2(0f, -ch * 0.06f); // centred on the slimes
            var ri = rays.GetComponent<Image>();
            ri.type = Image.Type.Simple;
            ri.raycastTarget = false;
            rays.AddComponent<UISpin>().degreesPerSecond = 10f;
            rays.transform.SetSiblingIndex(card.transform.GetSiblingIndex() + 1); // behind slimes/title
        }

        // fresh title
        WinText(
            popup,
            "WinTitle",
            "Level Complete!",
            font,
            TextPlum,
            48f,
            FontStyles.Bold,
            new Vector2(cw * 0.9f, ch * 0.14f),
            new Vector2(0f, ch * 0.20f)
        );
        // subtitle dropped — stars + rays carry the celebration now (kept clean/roomy)
        RemoveChild(popup.gameObject, "WinSub");

        // the two celebrating slimes, side by side (rays glow behind them)
        PlaceWinSlime(FindIn(popup, "Player1Image"), -cw * 0.22f, -ch * 0.06f);
        PlaceWinSlime(FindIn(popup, "Player2Image"), cw * 0.22f, -ch * 0.06f);

        // make BOTH slimes blink (random intervals → naturally desynced)
        WinSlimeBlink(FindIn(popup, "Player1Image"), "char_boy_front", "char_boy_blink");
        WinSlimeBlink(FindIn(popup, "Player2Image"), "char_girl_front", "char_girl_blink");

        // a heart between them — our glossy 3D red heart
        var heart = Heart3D();
        if (heart != null)
        {
            var hg = MakeImage(popup, "WinHeart", heart, Color.white);
            var hgrt = hg.GetComponent<RectTransform>();
            hgrt.anchorMin = hgrt.anchorMax = new Vector2(0.5f, 0.5f);
            hgrt.pivot = new Vector2(0.5f, 0.5f);
            hgrt.sizeDelta = new Vector2(50f, 50f);
            hgrt.anchoredPosition = new Vector2(0f, -ch * 0.06f);
            var hi = hg.GetComponent<Image>();
            hi.type = Image.Type.Simple;
            hi.preserveAspect = true;
            var hb = hg.AddComponent<FloatBob>(); // gentle heartbeat pulse
            hb.amount = 3f;
            hb.speed = 2.4f;
            hb.scaleAmount = 0.08f;
            hg.transform.SetAsLastSibling();
        }

        // remove the old static stars/sparkles from earlier passes
        for (int i = 0; i < 3; i++)
            RemoveChild(popup.gameObject, "WinStar" + i);
        for (int i = 0; i < 4; i++)
            RemoveChild(popup.gameObject, "WinSpark" + i);

        // ── Star-rating REVEAL row at the top: 3 gold stars pop in one-by-one (WinStars) ──
        RemoveChild(popup.gameObject, "WinStars");
        var starS = Load("star_3d.png") ?? Load("icon_star.png");
        if (starS != null)
        {
            var starsGo = new GameObject("WinStars", typeof(RectTransform), typeof(WinStars));
            starsGo.transform.SetParent(popup, false);
            var sgrt = starsGo.GetComponent<RectTransform>();
            sgrt.anchorMin = sgrt.anchorMax = new Vector2(0.5f, 0.5f);
            sgrt.pivot = new Vector2(0.5f, 0.5f);
            sgrt.sizeDelta = new Vector2(cw * 0.55f, 64f);
            sgrt.anchoredPosition = new Vector2(0f, ch * 0.38f);
            float[] sx = { -68f, 0f, 68f };
            float[] ssz = { 44f, 56f, 44f }; // centre star bigger
            float[] syo = { -4f, 8f, -4f }; // centre star raised → a gentle arc
            var goldCol = Color.white; // star_3d is already gold — show it natural
            var dimCol = new Color(0.5f, 0.48f, 0.5f, 0.7f); // desaturated "empty" slot
            for (int i = 0; i < 3; i++)
            {
                // empty slot (dim grey star, always visible)
                var slot = MakeImage(starsGo.transform, "slot" + i, starS, dimCol);
                var r = slot.GetComponent<RectTransform>();
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(ssz[i], ssz[i]);
                r.anchoredPosition = new Vector2(sx[i], syo[i]);
                slot.GetComponent<Image>().preserveAspect = true;
                // gold star over the slot (child 0), centred so WinStars can drop + scale it in
                var g = MakeImage(slot.transform, "gold", starS, goldCol);
                var gr = g.GetComponent<RectTransform>();
                gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f);
                gr.pivot = new Vector2(0.5f, 0.5f);
                gr.sizeDelta = new Vector2(ssz[i], ssz[i]);
                gr.anchoredPosition = Vector2.zero;
                g.GetComponent<Image>().preserveAspect = true;
                // white flash (child 1) — bursts as the gold lands
                var fl = MakeImage(slot.transform, "flash", starS, new Color(1f, 1f, 0.9f, 0f));
                var flr = fl.GetComponent<RectTransform>();
                flr.anchorMin = flr.anchorMax = new Vector2(0.5f, 0.5f);
                flr.pivot = new Vector2(0.5f, 0.5f);
                flr.sizeDelta = new Vector2(ssz[i] * 1.6f, ssz[i] * 1.6f);
                flr.anchoredPosition = Vector2.zero;
                fl.GetComponent<Image>().preserveAspect = true;
            }
            starsGo.transform.SetAsLastSibling();
        }

        // ── Confetti (celebratory) — falling hearts + stars behind the content, clipped to the card ──
        RemoveChild(popup.gameObject, "WinConfetti");
        var confetti = new GameObject(
            "WinConfetti",
            typeof(RectTransform),
            typeof(RectMask2D),
            typeof(WinConfetti)
        );
        confetti.transform.SetParent(popup, false);
        var cfrt = confetti.GetComponent<RectTransform>();
        cfrt.anchorMin = cfrt.anchorMax = new Vector2(0.5f, 0.5f);
        cfrt.pivot = new Vector2(0.5f, 0.5f);
        cfrt.sizeDelta = new Vector2(cw, ch); // card-sized → RectMask2D clips pieces to the card
        cfrt.anchoredPosition = Vector2.zero;
        confetti.transform.SetSiblingIndex(card.transform.GetSiblingIndex() + 1); // in front of card, behind content
        var cf = confetti.GetComponent<WinConfetti>();
        cf.pieces = new[] { Load("icon_heart.png"), Load("icon_star.png") };
        cf.colors = new[]
        {
            new Color(1f, 0.5f, 0.66f),
            new Color(1f, 0.82f, 0.3f),
            new Color(0.62f, 0.82f, 1f),
            new Color(1f, 0.78f, 0.86f),
        };
        cf.count = 7; // sparse + small so it's a gentle accent, not clutter
        cf.sizeRange = new Vector2(9f, 15f);
        cf.minSpeed = 40f;
        cf.maxSpeed = 85f;

        // reuse the HUD's round-button sprite so the win buttons match the ? / ‖ buttons exactly
        hudCircleSprite = null;
        var pbT = FindIn(root.transform, "PauseButton");
        if (pbT != null && pbT.TryGetComponent(out Image pbImg))
            hudCircleSprite = pbImg.sprite;

        // button row at the bottom: Menu · Replay · Next (all standardized in PlaceWinButton)
        PlaceWinButton(FindIn(popup, "ExitMenuButton"), -cw * 0.23f, -ch * 0.33f, "icon_home.png");
        PlaceWinButton(FindIn(popup, "ReplayButton"), 0f, -ch * 0.33f, "icon_retry.png");
        PlaceWinButton(FindIn(popup, "PlayButton"), cw * 0.23f, -ch * 0.33f, "icon_next.png");

        // celebratory pop+fade when the screen appears — bigger bounce than the other overlays
        EnsurePopIn(lc.gameObject, card.transform);
        if (lc.TryGetComponent(out PopInOnEnable winPop))
            winPop.startScale = 0.72f; // punchier entrance

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log(
            "[ArtRevamp] Win screen rebuilt (card + slimes + button row). Finish a level to review."
        );
    }

    // A fresh centred TMP child for the win card (positioned relative to the popup/card centre).
    static void WinText(
        Transform popup,
        string name,
        string text,
        TMP_FontAsset font,
        Color color,
        float maxSize,
        FontStyles style,
        Vector2 size,
        Vector2 pos
    )
    {
        RemoveChild(popup.gameObject, name);
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(popup, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            t.font = font;
        t.text = text;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true;
        t.fontSizeMin = 8f;
        t.fontSizeMax = maxSize;
        t.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        go.transform.SetAsLastSibling();
    }

    // Give a win-screen slime image an idle blink (swaps to its blink sprite now and then).
    static void WinSlimeBlink(Transform t, string frontName, string blinkName)
    {
        if (t == null || !t.TryGetComponent(out Image img))
            return;
        var front = LoadChar(frontName);
        var blink = LoadChar(blinkName);
        if (front != null)
            img.sprite = front;
        var ub = t.GetComponent<UIBlink>() ?? t.gameObject.AddComponent<UIBlink>();
        ub.normalSprite = front;
        ub.blinkSprite = blink;
    }

    static void PlaceWinSlime(Transform t, float x, float y)
    {
        if (t == null)
            return;
        if (!t.gameObject.activeSelf)
            t.gameObject.SetActive(true);
        t.localScale = Vector3.one; // the originals were scaled way up → reset so sizeDelta is the size
        foreach (var f in t.GetComponents<ContentSizeFitter>())
            Object.DestroyImmediate(f); // don't let a fitter override the size
        var rt = t.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(120f, 134f); // rays add presence behind them
        rt.anchoredPosition = new Vector2(x, y);
        if (t.TryGetComponent(out Image img))
            img.preserveAspect = true;
        var bob = t.GetComponent<FloatBob>() ?? t.gameObject.AddComponent<FloatBob>();
        bob.amount = 6f;
        bob.speed = x < 0 ? 1.5f : 1.85f; // desync the two slimes a touch
        bob.scaleAmount = 0.03f;
        t.SetAsLastSibling();
    }

    // Add a smooth pop+fade open transition to an overlay (CanvasGroup + PopInOnEnable).
    static void EnsurePopIn(GameObject overlay, Transform scaleTarget)
    {
        if (overlay == null)
            return;
        foreach (var f in overlay.GetComponents<FadeInOnEnable>())
            Object.DestroyImmediate(f); // PopIn supersedes a plain fade
        if (overlay.GetComponent<CanvasGroup>() == null)
            overlay.AddComponent<CanvasGroup>();
        var p = overlay.GetComponent<PopInOnEnable>() ?? overlay.AddComponent<PopInOnEnable>();
        if (scaleTarget != null)
            p.scaleTarget = scaleTarget as RectTransform;
    }

    static Sprite hudCircleSprite; // the round HUD-button sprite, reused for the win buttons

    // Standardize a win button to match the round HUD buttons: fixed 84×84, soft-pink circle bg, a
    // CONSTRAINED rose icon (the originals overflowed), consistent colour transition + bounce.
    static void PlaceWinButton(Transform bt, float x, float y, string iconName)
    {
        if (bt == null)
            return;
        var brt = bt.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(84f, 84f);
        brt.anchoredPosition = new Vector2(x, y);

        var b = bt.GetComponent<Button>();
        // background circle — prefer the "Capsule" child, else the button/targetGraphic image
        var cap = FindIn(bt, "Capsule");
        var bgImg =
            (cap != null ? cap.GetComponent<Image>() : null)
            ?? (b != null ? b.targetGraphic as Image : null)
            ?? bt.GetComponent<Image>();
        if (bgImg != null)
        {
            if (hudCircleSprite != null)
                bgImg.sprite = hudCircleSprite;
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;
            bgImg.color = new Color(1f, 0.72f, 0.8f);
            if (bgImg.transform != bt) // child → fill the button
            {
                var r = bgImg.rectTransform;
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = Vector2.zero;
                r.offsetMax = Vector2.zero;
            }
        }
        // drop the old shadow image (the kit card/circle carries its own look)
        var sh = FindIn(bt, "BtnShadow");
        if (sh != null && sh.TryGetComponent(out Image shi))
        {
            if (hudCircleSprite != null)
                shi.sprite = hudCircleSprite;
            shi.color = new Color(0f, 0f, 0f, 0.12f);
        }
        // icon — CONSTRAINED to a fixed size (the Next arrow was overflowing the card)
        var iconT = FindIn(bt, "Icon");
        if (iconT != null && iconT.TryGetComponent(out Image ico))
        {
            var ir = ico.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            ir.sizeDelta = new Vector2(38f, 38f); // smaller so the wide » never clips the circle
            ir.anchoredPosition = Vector2.zero;
            ir.localScale = Vector3.one;
            ico.color = new Color(0.72f, 0.24f, 0.40f); // rose
            ico.type = Image.Type.Simple; // not Sliced (which squeezed the round icon)
            ico.preserveAspect = true;
            var ic = Load(iconName) ?? Load("icon_replay.png"); // retry falls back to the clean kit arrow
            if (ic != null)
                ico.sprite = ic;
            iconT.SetAsLastSibling();
        }
        if (b != null)
        {
            if (bgImg != null)
                b.targetGraphic = bgImg;
            StyleHudButtonColors(b); // selected=normal + nav None (won't light up when win appears)
        }
        if (bt.GetComponent<ButtonBounce>() == null)
            bt.gameObject.AddComponent<ButtonBounce>();
        bt.SetAsLastSibling();
    }

    static void AddText(
        Transform parent,
        string name,
        string text,
        TMP_FontAsset font,
        Color color,
        float maxSize,
        FontStyles style,
        Vector2 aMin,
        Vector2 aMax,
        TextAlignmentOptions align
    )
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.text = text;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = true;
        tmp.enableAutoSizing = true; // fit to rect, capped at maxSize
        tmp.fontSizeMin = 8f;
        tmp.fontSizeMax = maxSize;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Clear a button's onClick and wire it to SetActive(value) on a target.
    static void RewireToggle(Transform buttonT, GameObject target, bool value)
    {
        if (buttonT == null || !buttonT.TryGetComponent(out Button b))
            return;
        for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(b.onClick, i);
        UnityEventTools.AddBoolPersistentListener(b.onClick, target.SetActive, value);
    }

    // Reskin the level-button PREFAB → updates all 9 instances at once.
    static void ReskinLevelButtonPrefab()
    {
        const string path = "Assets/_Project/Prefabs/Level00Button.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);

        var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        var cream = new Color(1f, 0.96f, 0.92f); // soft cream tile — matches the kit cards
        var rose = TextPlum; // wine-rose number, same as the headings

        var img = root.GetComponent<Image>();
        if (img == null && root.TryGetComponent(out Button probe))
            img = probe.targetGraphic as Image;
        if (img != null)
        {
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            img.color = cream;
            img.pixelsPerUnitMultiplier = 0.45f; // bigger radius → cute rounded tile

            // soft coral outline so the cream tile reads against the pink background
            var outline = root.GetComponent<Outline>() ?? root.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.6f, 0.7f, 0.5f);
            outline.effectDistance = new Vector2(3f, -3f);
        }
        if (root.TryGetComponent(out Button b))
        {
            b.transition = Selectable.Transition.ColorTint;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 0.92f, 0.95f);
            cb.pressedColor = new Color(0.9f, 0.78f, 0.84f);
            cb.disabledColor = new Color(0.72f, 0.72f, 0.72f, 0.65f); // locked levels
            cb.fadeDuration = 0.08f;
            b.colors = cb;
        }
        foreach (var t in root.GetComponentsInChildren<Text>(true))
            t.color = rose;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            t.color = rose;
            t.enableVertexGradient = false; // kill the harsh red gradient on the numbers
            if (t.font != null)
                t.fontSharedMaterial = t.font.material;
        }

        // Lock icon: shown on locked levels (hides the number); toggled at runtime
        var numberLabel = FindIn(root.transform, "Text");
        var lockT = FindIn(root.transform, "Lock");
        GameObject lockGo;
        if (lockT == null)
        {
            lockGo = new GameObject(
                "Lock",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            lockGo.transform.SetParent(root.transform, false);
        }
        else
        {
            lockGo = lockT.gameObject;
        }
        var li = lockGo.GetComponent<Image>();
        li.sprite = Load("ui_lock.png") ?? Load("icon_lock.png"); // warm rose padlock (replaces the blue one)
        li.preserveAspect = true;
        li.raycastTarget = false;
        li.color = Color.white; // ui_lock is already on-palette — no tint needed
        var lrt = lockGo.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0.3f, 0.22f);
        lrt.anchorMax = new Vector2(0.7f, 0.78f);
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        lockGo.SetActive(false); // runtime shows it only when the level is locked

        var lockComp = root.GetComponent<LevelLockIcon>() ?? root.AddComponent<LevelLockIcon>();
        lockComp.lockIcon = lockGo;
        lockComp.numberLabel = numberLabel ? numberLabel.gameObject : null;

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[ArtRevamp] Level00Button prefab reskinned (updates all instances).");
    }

    // Recolor the scene-transition fade from harsh black to a soft pink (edits the prefab).
    static void ImproveTransition()
    {
        const string path = "Assets/_Project/Prefabs/Level Loader.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var bs = FindIn(root.transform, "BlackScreen");
        if (bs && bs.TryGetComponent(out Image bi))
        {
            bi.color = new Color(1f, 0.82f, 0.88f, 1f); // soft pink (alpha driven by CanvasGroup)
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("[ArtRevamp] transition fade recolored to soft pink.");
        }
        else
        {
            Debug.LogWarning("[ArtRevamp] BlackScreen Image not found in Level Loader prefab.");
        }
        PrefabUtility.UnloadPrefabContents(root);
    }

    // Replace the black gameplay background with the kawaii menu sky. Edits the shared
    // Main Camera prefab, so a single run reskins all 9 levels at once. Idempotent.
    [MenuItem("Tools/Soulmates/Apply Gameplay Background")]
    public static void ApplyGameplayBackground()
    {
        // Gameplay backdrop = a soft GRADIENT (sky colours) with drifting clouds over it — cleaner than
        // the busy static image. Falls back to bg_game.png if the gradient isn't there.
        SetSprite("bg_gradient.png", null);
        SetSprite("bg_game.png", null);
        var sky = Load("bg_gradient.png") ?? Load("bg_game.png");
        if (sky == null)
        {
            Debug.LogWarning(
                "[ArtRevamp] no bg_gradient.png / bg_game.png in Sprites/UI/ — gameplay background not applied."
            );
            return;
        }

        const string camPath = "Assets/_Project/Prefabs/Main Camera.prefab";
        var cam = PrefabUtility.LoadPrefabContents(camPath);

        // Soft sky tone so any sliver the sprite doesn't cover (extreme aspect ratios) blends in
        // instead of showing the old black.
        cam.TryGetComponent(out Camera c);
        if (c != null)
        {
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.97f, 0.85f, 0.91f); // pale pink, matches the sky base
        }

        // Backdrop is a child of the camera so it follows the view in every level scene.
        var bgT = FindIn(cam.transform, "SkyBackground");
        var bg =
            bgT != null ? bgT.gameObject : new GameObject("SkyBackground", typeof(SpriteRenderer));
        if (bgT == null)
            bg.transform.SetParent(cam.transform, false);

        var sr = bg.GetComponent<SpriteRenderer>();
        sr.sprite = sky;
        sr.sortingOrder = -1000; // behind the tilemap + characters (single "Default" sorting layer)
        var unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        if (unlit != null)
            sr.sharedMaterial = unlit; // unlit → full-bright, ignores the players' 2D glow lights

        // Sit in front of the orthographic camera and scale to cover the view (with a little margin).
        bg.transform.localPosition = new Vector3(0f, 0f, 10f); // ahead of the cam, within near/far
        bg.transform.localRotation = Quaternion.identity;
        float orthoSize = c != null ? c.orthographicSize : 4f;
        float viewH = 2f * orthoSize; // world-units tall
        float viewW = viewH * (16f / 9f); // assume ~16:9; margin + clear color cover other ratios
        const float margin = 1.12f;
        Vector2 native = sky.rect.size / sky.pixelsPerUnit;
        float scale = Mathf.Max((viewW * margin) / native.x, (viewH * margin) / native.y); // cover
        bg.transform.localScale = new Vector3(scale, scale, 1f);

        // Drifting clouds in front of the static sky so the play screen feels alive, not frozen.
        // clouds.png is sliced into separate puffs (Multiple mode); CloudDrift scatters + drifts a few
        // at runtime (visible in Play mode). Tune on the component.
        var cloudReps = AssetDatabase.LoadAllAssetRepresentationsAtPath(
            "Assets/_Project/Sprites/clouds.png"
        );
        var cloudList = new System.Collections.Generic.List<Sprite>();
        foreach (var r in cloudReps)
            if (r is Sprite cs)
                cloudList.Add(cs);

        var clT = FindIn(cam.transform, "Clouds");
        var clouds = clT != null ? clT.gameObject : new GameObject("Clouds");
        if (clT == null)
            clouds.transform.SetParent(cam.transform, false);
        clouds.transform.localPosition = new Vector3(0f, viewH * 0.12f, 9.5f); // centred sky, ahead of bg
        clouds.transform.localRotation = Quaternion.identity;
        clouds.transform.localScale = Vector3.one;
        var drift = clouds.GetComponent<CloudDrift>() ?? clouds.AddComponent<CloudDrift>();
        drift.cloudSprites = cloudList.ToArray();
        drift.material = unlit;
        drift.sortingOrder = -900; // in front of sky (-1000), behind the gameplay tiles (0)
        drift.baseSpeed = 0.35f; // gentle; raise for faster clouds
        drift.alpha = 0.7f; // the clouds are the main sky feature now (gradient has none baked in)
        drift.count = 18; // fuller sky
        drift.spanX = viewW * 1.3f; // scatter a bit wider than the view; wrap off-screen
        drift.spanY = viewH * 0.34f; // spread across most of the sky
        if (cloudList.Count > 0)
        {
            float cnw = cloudList[0].rect.width / cloudList[0].pixelsPerUnit; // a puff's native width
            drift.scale = cnw > 0f ? (viewW * 0.16f) / cnw : 1f; // each cloud ≈ 16% of view wide
        }

        PrefabUtility.SaveAsPrefabAsset(cam, camPath);
        PrefabUtility.UnloadPrefabContents(cam);
        Debug.Log(
            "[ArtRevamp] Gameplay background + drifting clouds applied to Main Camera — Play a level to see the drift."
        );
    }

    // Brighten the murky ground. The game ALREADY ships a global Light2D on the Game Controller
    // prefab (in every level) — it was just dim (intensity 0.33). So we bump THAT one rather than
    // adding our own (two globals on the same layer triggers URP's "more than one global light"
    // warning). Also removes any duplicate global light a previous version of this command added.
    [MenuItem("Tools/Soulmates/Apply Level Lighting")]
    public static void ApplyLevelLighting()
    {
        const float intensity = 0.85f; // soft, even base; keeps the colored point-light glows readable
        var warm = new Color(0.99f, 0.98f, 0.83f); // matches the existing warm global tone

        // 1) Remove the stray global light an earlier run added to the Grid prefab.
        const string gridPath = "Assets/_Project/Prefabs/Grid.prefab";
        var grid = PrefabUtility.LoadPrefabContents(gridPath);
        var stray = FindIn(grid.transform, "Global Light 2D");
        if (stray != null)
        {
            Object.DestroyImmediate(stray.gameObject);
            PrefabUtility.SaveAsPrefabAsset(grid, gridPath);
            Debug.Log("[ArtRevamp] Removed the duplicate global light from the Grid prefab.");
        }
        PrefabUtility.UnloadPrefabContents(grid);

        // 2) Brighten the existing global light on the Game Controller prefab (present in all levels).
        const string gcPath = "Assets/_Project/Prefabs/Game Controller.prefab";
        var gc = PrefabUtility.LoadPrefabContents(gcPath);
        int n = 0;
        foreach (var light in gc.GetComponentsInChildren<Light2D>(true))
        {
            if (light.lightType != Light2D.LightType.Global)
                continue;
            light.enabled = true;
            light.color = warm;
            light.intensity = intensity;
            n++;
        }
        PrefabUtility.SaveAsPrefabAsset(gc, gcPath);
        PrefabUtility.UnloadPrefabContents(gc);
        Debug.Log(
            $"[ArtRevamp] Global light brightened to {intensity} (×{n}). The duplicate is gone — reopen a level."
        );
    }

    // Replace the static pop-in heart with a heart-SHAPED puff of soft hearts that rises, expands
    // and fades like smoke (coffee-smoke vibe). Particles emit inside the heart sprite's silhouette,
    // so the cloud is heart-shaped; they then drift up with turbulence and fade out. Lives on a
    // "LoveSmoke" child of the stack's Heart object (which GameController toggles when slimes meet).
    [MenuItem("Tools/Soulmates/Apply Love Smoke")]
    public static void ApplyLoveSmoke()
    {
        // Prepare the smoke material FIRST, before loading the prefab (AssetDatabase work while a
        // prefab is open via LoadPrefabContents is what broke this before). Uses a soft round puff
        // texture (generated once) so the particles read as wispy smoke, not hard sprites.
        var puff = EnsureSoftPuffTexture();
        const string matPath = "Assets/_Project/Sprites/LoveSmoke.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Sprites/Default")); // unlit transparent, works in URP
            AssetDatabase.CreateAsset(mat, matPath);
        }
        if (puff != null)
            mat.mainTexture = puff;
        EditorUtility.SetDirty(mat);

        // Second material: the puffy 3D candy heart that floats up out of the smoke (also prepared
        // BEFORE the prefab is opened, for the same AssetDatabase-timing reason).
        const string heartTexPath = "Assets/_Project/Sprites/heart_3d.png";
        AssetDatabase.ImportAsset(heartTexPath);
        var heartTex = AssetDatabase.LoadAssetAtPath<Texture2D>(heartTexPath);
        const string heartMatPath = "Assets/_Project/Sprites/LoveHearts.mat";
        var heartMat = AssetDatabase.LoadAssetAtPath<Material>(heartMatPath);
        if (heartMat == null)
        {
            heartMat = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(heartMat, heartMatPath);
        }
        if (heartTex != null)
            heartMat.mainTexture = heartTex;
        EditorUtility.SetDirty(heartMat);
        AssetDatabase.SaveAssets();

        const string gridPath = "Assets/_Project/Prefabs/Grid.prefab";
        var grid = PrefabUtility.LoadPrefabContents(gridPath);
        var set = FindIn(grid.transform, "Player1+Player2");
        if (set == null || set.childCount < 3)
        {
            Debug.LogWarning("[ArtRevamp] Player1+Player2 stack (with Heart) not found.");
            PrefabUtility.UnloadPrefabContents(grid);
            return;
        }
        var heart = set.GetChild(2).gameObject; // [0]=clone1 [1]=clone2 [2]=heart

        // hide the old static heart sprite + its scale animator
        if (heart.TryGetComponent(out SpriteRenderer hsr))
            hsr.enabled = false;
        if (heart.TryGetComponent(out Animator han))
            han.enabled = false;

        // Rebuild the effect child cleanly each run so a half-built one can't linger (that orphaned
        // a ParticleSystemRenderer with no ParticleSystem before → the runtime error you saw).
        RemoveChild(heart, "LoveSmoke");
        var smoke = new GameObject("LoveSmoke", typeof(ParticleSystem)); // system + renderer together
        smoke.transform.SetParent(heart.transform, false);
        smoke.transform.localPosition = Vector3.zero;

        var ps = smoke.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSpeed = 0f; // motion comes from velocityOverLifetime
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.85f); // big soft puffs to cover the swap
        main.startColor = new Color(1f, 0.9f, 0.93f, 1f); // soft warm white with a love tint
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 250;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)40) }); // one quick dense poof

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle; // central cluster; big puffs spread to cover
        shape.radius = 0.3f;

        // Rise straight UP with only a small sideways spread (the old wide ±X / slight-down Y looked
        // like it drifted off to the side). All three axes must share the SAME curve mode.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        vel.y = new ParticleSystem.MinMaxCurve(0.15f, 0.45f); // clean upward drift
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.2f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.4f; // wispy, smoke-like turbulence

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.8f, 1f, 2f)); // expand big

        // Alpha ramps up FAST (covers the slime→hug swap within ~0.1s), holds, then fades to reveal.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.96f), 0f),
                new GradientColorKey(new Color(1f, 0.82f, 0.88f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f, 0.12f),
                new GradientAlphaKey(0.95f, 0.5f),
                new GradientAlphaKey(0f, 1f),
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var psr = smoke.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.material = mat;
        psr.sortingOrder = 100; // ABOVE the slimes/hug, so the smoke masks the transition

        // ── Floating 3D hearts: a few puffy candy hearts that pop and rise OUT of the smoke ──
        RemoveChild(heart, "LoveHearts");
        var hearts = new GameObject("LoveHearts", typeof(ParticleSystem));
        hearts.transform.SetParent(heart.transform, false);
        hearts.transform.localPosition = Vector3.zero;
        var hp = hearts.GetComponent<ParticleSystem>();
        hp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var hmain = hp.main;
        hmain.duration = 1.4f;
        hmain.loop = false;
        hmain.startLifetime = 1.7f;
        hmain.startSpeed = 0f;
        hmain.startSize = 0.8f; // ONE big hero heart (≈ the couple's size; tune up for bigger)
        hmain.startRotation = 0f;
        hmain.startColor = Color.white; // let the heart texture supply the pink
        hmain.simulationSpace = ParticleSystemSimulationSpace.Local;
        hmain.maxParticles = 4;
        hmain.playOnAwake = true;

        var hem = hp.emission;
        hem.enabled = true;
        hem.rateOverTime = 0f;
        hem.SetBursts(new[] { new ParticleSystem.Burst(0.1f, (short)1) }); // a single heart

        var hshape = hp.shape;
        hshape.enabled = false; // emit dead-centre above the couple

        var hvel = hp.velocityOverLifetime;
        hvel.enabled = true;
        hvel.space = ParticleSystemSimulationSpace.Local;
        hvel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        hvel.y = new ParticleSystem.MinMaxCurve(0.45f, 0.45f); // float gently straight up
        hvel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Big bouncy pop in, hold, then a slight shrink as it fades out.
        var hsize = hp.sizeOverLifetime;
        hsize.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.1f),
            new Keyframe(0.22f, 1.15f), // overshoot = bouncy pop
            new Keyframe(0.4f, 1f),
            new Keyframe(1f, 0.9f)
        );
        hsize.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // keep the 3D heart UPRIGHT (no tilt/spin)
        var hrot = hp.rotationOverLifetime;
        hrot.enabled = false;

        var hcol = hp.colorOverLifetime;
        hcol.enabled = true;
        var hgrad = new Gradient();
        hgrad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0f, 1f),
            }
        );
        hcol.color = new ParticleSystem.MinMaxGradient(hgrad);

        var hpsr = hearts.GetComponent<ParticleSystemRenderer>();
        hpsr.renderMode = ParticleSystemRenderMode.Billboard;
        hpsr.material = heartMat;
        hpsr.sortingOrder = 101; // above the smoke, so the hearts read clearly on top

        PrefabUtility.SaveAsPrefabAsset(grid, gridPath);
        PrefabUtility.UnloadPrefabContents(grid);
        Debug.Log(
            "[ArtRevamp] Love smoke + floating 3D hearts applied. Stack the two slimes to see it."
        );
    }

    // A soft round white puff texture for smoke particles (generated once, saved as a PNG asset).
    static Texture2D EnsureSoftPuffTexture()
    {
        const string path = "Assets/_Project/Sprites/smoke_puff.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null)
            return existing;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - r) / r;
            float dy = (y + 0.5f - r) / r;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(1f - d);
            a = a * a * (3f - 2f * a); // smoothstep falloff → soft edges
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // The square rune blocks stand too upright vs. the ground's 2:1 isometric diamonds, so we
    // vertically compress them to lie flat in the grid plane. Tweak if they still look off.
    const float RuneFlatten = 0.55f; // localScale.y; 1 = unchanged, lower = flatter on the ground

    // Swap the win-tile diamonds for Norse rune tiles — one rune per soulmate — and recolor each
    // tile's glow Light2D + particles to MATCH that soulmate (boy = blue, girl = pink), instead of
    // the old yellow/cyan. The light + particles are child objects; the tile material is untouched.
    // Drop these into Sprites/:  rune_tile_1.png → WinTile1 (boy),  rune_tile_2.png → WinTile2 (girl).
    [MenuItem("Tools/Soulmates/Apply Win Tile Rune")]
    public static void ApplyWinTileRune()
    {
        var map = new[]
        {
            ("WinTile1", "Assets/_Project/Sprites/rune_tile_1.png", new Color(0.557f, 0.773f, 1f)), // boy blue #8EC5FF
            ("WinTile2", "Assets/_Project/Sprites/rune_tile_2.png", new Color(1f, 0.702f, 0.776f)), // girl pink #FFB3C6
        };

        const string gridPath = "Assets/_Project/Prefabs/Grid.prefab";
        var grid = PrefabUtility.LoadPrefabContents(gridPath);
        int applied = 0;
        foreach (var (tileName, runePath, tint) in map)
        {
            ConfigureWorldSprite(runePath); // import as a single sprite, sized to one tile
            var rune = AssetDatabase.LoadAssetAtPath<Sprite>(runePath);
            if (rune == null)
            {
                Debug.LogWarning($"[ArtRevamp] {runePath} not found — {tileName} left unchanged.");
                continue;
            }
            var t = FindIn(grid.transform, tileName);
            if (t == null || !t.TryGetComponent(out SpriteRenderer sr))
                continue;

            sr.sprite = rune;
            t.localScale = new Vector3(1f, RuneFlatten, 1f); // lie flat on the iso ground

            // recolor the glow + sparkles to match the soulmate (child objects), and make the glow
            // BREATHE so the goal tile clearly "calls" the player (reuses the slimes' breathing-light).
            foreach (var light in t.GetComponentsInChildren<Light2D>(true))
            {
                light.color = tint;
                var lc =
                    light.GetComponent<PlayerLightController>()
                    ?? light.gameObject.AddComponent<PlayerLightController>();
                lc.minIntensity = 0.55f;
                lc.maxIntensity = 1.0f; // clear bright-dim pulse
                lc.breathingSpeed = 2.2f; // gentle, noticeable
                lc.minRadius = 1.3f;
                lc.maxRadius = 1.9f;
                lc.useRandomOffset = true;
            }
            foreach (var ps in t.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = tint;
            }
            applied++;
        }
        PrefabUtility.SaveAsPrefabAsset(grid, gridPath);
        PrefabUtility.UnloadPrefabContents(grid);
        Debug.Log(
            $"[ArtRevamp] Win tiles updated — {applied}/2 runes applied (recolored to match soulmates). Play to review."
        );
    }

    // Import a world-space sprite (e.g. a goal tile) at center pivot, scaled so its full width is
    // ~1 world unit — one isometric cell.
    static void ConfigureWorldSprite(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti))
        {
            Debug.LogWarning("[ArtRevamp] world sprite not found: " + path);
            return;
        }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Center;
        s.spritePixelsPerUnit = tex != null ? tex.width : 256f; // full width = 1 tile
        ti.SetTextureSettings(s);
        ti.SaveAndReimport();
    }

    // ---- 1. Import settings ----------------------------------------------
    static void ConfigureImporters()
    {
        // 9-slice borders are Vector4(left, bottom, right, top)
        SetSprite("ui_button.png", new Vector4(168, 136, 168, 136));
        SetSprite("ui_panel.png", new Vector4(370, 265, 370, 265));
        // modern kit (sliced from ui_kits.png) — the current UI look
        SetSprite("ui_card.png", new Vector4(70, 70, 70, 70)); // soft rounded panel
        SetSprite("ui_btn.png", new Vector4(85, 78, 85, 78)); // coral pill (rounded ends)
        SetSprite("ui_knob.png", null); // round slider knob

        string[] simple =
        {
            "bg_menu",
            "bg_game",
            "ui_logo_3",
            "ui_logo_stacked",
            "ui_logo_wide",
            "icon_play",
            "icon_pause",
            "icon_gear",
            "icon_music",
            "icon_sound",
            "icon_replay",
            "icon_next",
            "icon_home",
            "icon_lock",
            "ui_lock",
            "icon_heart",
            "key_arrow", // white chevron used as the directional key legend in the Help diagram
            "key_cap", // baked 3D candy keycap (lit top + extruded side wall)
            "icon_retry", // ⟲ circular arrow (uniform font icon set)
            "icon_help", // ? (uniform font icon set)
            "icon_star", // ★ win-screen rating star (flat, used for confetti)
            "star_3d", // glossy 3D gold star (win rating + timer)
            "ray_burst", // sunburst rays behind the win slimes
            "sparkle", // soft twinkle emitted at the timer bar's leading edge
        };
        foreach (var f in simple)
            SetSprite(f + ".png", null);

        AssetDatabase.Refresh();
    }

    static void SetSprite(string file, Vector4? border)
    {
        var path = UIDir + file;
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti))
        {
            Debug.LogWarning("[ArtRevamp] sprite not found: " + path);
            return;
        }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        if (border.HasValue)
            ti.spriteBorder = border.Value;
        ti.SaveAndReimport();
    }

    static Sprite Load(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(UIDir + file);

    // Our glossy 3D red heart (shared by the win + credit screens); falls back to the flat icon.
    static Sprite Heart3D() =>
        AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/heart_3d.png")
        ?? Load("icon_heart.png");

    // Revamp the end-of-game credits into the Soft Candy look: kawaii sky, a soft card, the two
    // soulmates with a floating heart between them, styled thanks/made-by text, a coral Main Menu
    // pill, and gentle heart/star confetti. Everything reuses the shared UI primitives.
    [MenuItem("Tools/Soulmates/Apply Credit Screen")]
    public static void ApplyCreditScreen()
    {
        var scene = EditorSceneManager.OpenScene(CreditScene, OpenSceneMode.Single);
        // Parent onto the CONTENT canvas (the one holding the credits), found via an existing child —
        // NOT FindObjectOfType<Canvas>(), which can return the LevelLoader's high-sort fade canvas and
        // make our card render on top of (and hide) all the content.
        var anchorGo = Find(scene, "Background") ?? Find(scene, "Thanks");
        if (anchorGo == null || anchorGo.transform.parent == null)
        {
            Debug.LogWarning("[ArtRevamp] Credit content canvas not found.");
            return;
        }
        var root = anchorGo.transform.parent;
        var scaler =
            root.GetComponentInParent<CanvasScaler>() ?? Object.FindObjectOfType<CanvasScaler>();

        // Remove leftovers from a previous run SCENE-WIDE — an earlier version parented these onto the
        // fade canvas, so RemoveChild(root, …) alone can't reach them and the old card keeps covering
        // everything.
        foreach (
            var junk in new[] { "CreditCard", "CreditCardShadow", "CreditHeart", "CreditConfetti" }
        )
        {
            var g = Find(scene, junk);
            while (g != null)
            {
                Object.DestroyImmediate(g);
                g = Find(scene, junk);
            }
        }
        float refW = scaler ? scaler.referenceResolution.x : 1920f;
        float refH = scaler ? scaler.referenceResolution.y : 1080f;
        var rose = TextPlum;
        float cw = refW * 0.52f,
            ch = refH * 0.8f;

        // Background → kawaii sky (full-screen), behind everything.
        var bgGo = Find(scene, "Background");
        if (bgGo != null && bgGo.TryGetComponent(out Image bgImg))
        {
            bgImg.sprite = Load("bg_menu.png");
            bgImg.type = Image.Type.Simple;
            bgImg.color = Color.white;
            var rt = bgImg.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            bgGo.transform.SetAsFirstSibling();
        }

        // Soft cream card behind the content.
        var card = AddCard(
            root,
            "CreditCard",
            new Vector2(0.24f, 0.09f),
            new Vector2(0.76f, 0.91f)
        );

        // Gentle heart/star confetti, clipped to the card.
        RemoveChild(root.gameObject, "CreditConfetti");
        var confetti = new GameObject(
            "CreditConfetti",
            typeof(RectTransform),
            typeof(RectMask2D),
            typeof(WinConfetti)
        );
        confetti.transform.SetParent(root, false);
        var cfrt = confetti.GetComponent<RectTransform>();
        cfrt.anchorMin = cfrt.anchorMax = new Vector2(0.5f, 0.5f);
        cfrt.pivot = new Vector2(0.5f, 0.5f);
        cfrt.sizeDelta = new Vector2(cw, ch);
        cfrt.anchoredPosition = Vector2.zero;
        confetti.transform.SetSiblingIndex(card.transform.GetSiblingIndex() + 1);
        var cf = confetti.GetComponent<WinConfetti>();
        cf.pieces = new[] { Load("icon_heart.png"), Load("icon_star.png") };
        cf.colors = new[]
        {
            new Color(1f, 0.5f, 0.66f),
            new Color(1f, 0.82f, 0.3f),
            new Color(0.62f, 0.82f, 1f),
            new Color(1f, 0.78f, 0.86f),
        };
        cf.count = 9;
        cf.sizeRange = new Vector2(9f, 16f);

        // Title
        var thanks = Find(scene, "Thanks");
        if (thanks != null)
        {
            CenterCreditText(
                thanks,
                new Vector2(0f, ch * 0.377f),
                new Vector2(cw * 0.92f, ch * 0.2f)
            );
            if (thanks.TryGetComponent(out TMP_Text tt))
            {
                tt.text = "THANKS FOR PLAYING!";
                tt.color = rose;
                tt.fontStyle = FontStyles.Bold;
                tt.enableAutoSizing = false;
                tt.fontSize = refH * 0.062f;
                tt.alignment = TextAlignmentOptions.Center;
            }
        }

        // The two soulmates (NEW slime art) + a floating heart between them.
        PlaceCreditSlime(
            Find(scene, "Player1Image"),
            -cw * 0.21f,
            ch * 0.088f,
            1.5f,
            LoadChar("char_boy_front")
        );
        PlaceCreditSlime(
            Find(scene, "Player2Image"),
            cw * 0.21f,
            ch * 0.088f,
            1.85f,
            LoadChar("char_girl_front")
        );
        // ...and let them blink, same as the win screen.
        WinSlimeBlink(Find(scene, "Player1Image")?.transform, "char_boy_front", "char_boy_blink");
        WinSlimeBlink(Find(scene, "Player2Image")?.transform, "char_girl_front", "char_girl_blink");

        RemoveChild(root.gameObject, "CreditHeart");
        var heart = MakeImage(root, "CreditHeart", Heart3D(), Color.white);
        var hrt = heart.GetComponent<RectTransform>();
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
        hrt.pivot = new Vector2(0.5f, 0.5f);
        hrt.sizeDelta = new Vector2(84f, 84f);
        hrt.anchoredPosition = new Vector2(0f, ch * 0.138f);
        heart.GetComponent<Image>().preserveAspect = true;
        var hbob = heart.AddComponent<FloatBob>();
        hbob.amount = 8f;
        hbob.speed = 1.6f;
        hbob.scaleAmount = 0.07f;
        heart.transform.SetAsLastSibling();

        // "MADE BY" + the developer handle.
        var madeBy = Find(scene, "MadeBy");
        if (madeBy != null)
        {
            CenterCreditText(
                madeBy,
                new Vector2(0f, -ch * 0.123f),
                new Vector2(cw * 0.8f, ch * 0.09f)
            );
            if (madeBy.TryGetComponent(out TMP_Text mt))
            {
                mt.text = "MADE BY";
                mt.color = rose;
                mt.enableAutoSizing = false;
                mt.fontSize = refH * 0.03f;
                mt.characterSpacing = 8f;
                mt.alignment = TextAlignmentOptions.Center;
            }
        }
        var devName = Find(scene, "DeveloperName");
        if (devName != null)
        {
            CenterCreditText(
                devName,
                new Vector2(0f, -ch * 0.202f),
                new Vector2(cw * 0.8f, ch * 0.14f)
            );
            if (devName.TryGetComponent(out TMP_Text dt))
            {
                dt.color = Accent;
                dt.fontStyle = FontStyles.Bold;
                dt.enableAutoSizing = false;
                dt.fontSize = refH * 0.052f;
                dt.alignment = TextAlignmentOptions.Center;
            }
        }

        // Main Menu → coral pill with a white centred label.
        var menuBtn = Find(scene, "MainMenuButton");
        if (menuBtn != null)
        {
            var brt = menuBtn.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(refW * 0.26f, refH * 0.1f);
            brt.anchoredPosition = new Vector2(0f, -ch * 0.363f); // clear of both the name and the bottom edge
            RemoveChild(menuBtn, "Icon");
            SkinButton(menuBtn, null, false);
            foreach (var t in menuBtn.GetComponentsInChildren<TMP_Text>(true))
            {
                t.color = Color.white;
                t.alignment = TextAlignmentOptions.Center;
            }
            foreach (var t in menuBtn.GetComponentsInChildren<Text>(true))
            {
                t.color = Color.white;
                t.alignment = TextAnchor.MiddleCenter;
            }
            menuBtn.transform.SetAsLastSibling();
        }

        // Explicit layering (all in the SAME content canvas now): sky at the back, then card + its
        // shadow + confetti, then every content object on top (they were SetAsLastSibling above).
        if (bgGo != null)
            bgGo.transform.SetAsFirstSibling();
        var cardShadow = Find(scene, "CreditCardShadow");
        if (cardShadow != null)
            cardShadow.transform.SetSiblingIndex(1);
        card.transform.SetSiblingIndex(2);
        confetti.transform.SetSiblingIndex(3);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ArtRevamp] Credit screen revamped (Soft Candy). Open Credit.unity to review.");
    }

    static void CenterCreditText(GameObject go, Vector2 pos, Vector2 size)
    {
        foreach (var f in go.GetComponents<ContentSizeFitter>())
            Object.DestroyImmediate(f); // don't let a fitter fight our size
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        rt.localScale = Vector3.one;
        go.transform.SetAsLastSibling();
    }

    static void PlaceCreditSlime(GameObject go, float x, float y, float speed, Sprite sprite)
    {
        if (go == null)
            return;
        if (!go.activeSelf)
            go.SetActive(true);
        foreach (var f in go.GetComponents<ContentSizeFitter>())
            Object.DestroyImmediate(f);
        var rt = go.GetComponent<RectTransform>();
        rt.localScale = Vector3.one;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(168f, 187f);
        rt.anchoredPosition = new Vector2(x, y);
        if (go.TryGetComponent(out Image img))
        {
            if (sprite != null)
                img.sprite = sprite; // NEW slime art (was old character / a broken white sprite)
            img.color = Color.white;
            img.preserveAspect = true;
        }
        var bob = go.GetComponent<FloatBob>() ?? go.AddComponent<FloatBob>();
        bob.amount = 8f;
        bob.speed = speed;
        bob.scaleAmount = 0.03f;
        go.transform.SetAsLastSibling();
    }

    // ---- 2. Scene wiring -------------------------------------------------
    static void WireMenu()
    {
        var scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);

        var btn = Load("ui_button.png");
        var bg = Load("bg_menu.png");
        var logo = Load("ui_logo_3.png");

        // Background → full-screen kawaii sky
        var bgGo = Find(scene, "Background");
        if (bgGo && bgGo.TryGetComponent(out Image bgImg))
        {
            bgImg.sprite = bg;
            bgImg.type = Image.Type.Simple;
            var rt = bgImg.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            Debug.LogWarning("[ArtRevamp] 'Background' Image not found.");
        }

        // Canvas reference size (shared by buttons + logo)
        var scaler = Object.FindObjectOfType<CanvasScaler>();
        float refW = scaler ? scaler.referenceResolution.x : 1920f;
        float refH = scaler ? scaler.referenceResolution.y : 1080f;

        // Buttons → uniform size, evenly spaced in the lower-center, with a capsule
        float bw = refW * 0.3f; // trimmer, more refined than the big slabs
        float bh = refH * 0.095f; // slightly shorter so the gaps can breathe without pushing too low
        float gap = refH * 0.165f; // gap clearly > corner radius → reads as 3 separate buttons, not a joined group
        float topY = refH * 0.02f; // first button just above centre, clearing the logo above

        var mainButtons = new[] { "PlayButton", "OptionButton", "QuitButton" };
        for (int i = 0; i < mainButtons.Length; i++)
        {
            var go = Find(scene, mainButtons[i]);
            if (!go)
            {
                Debug.LogWarning("[ArtRevamp] button not found: " + mainButtons[i]);
                continue;
            }
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(bw, bh);
            rt.anchoredPosition = new Vector2(0f, topY - i * gap);
            RemoveChild(go, "Icon"); // clean text-only buttons
            SkinButton(go, btn, false);
        }

        // (Back button is handled in WireOptions)

        // Title logo (find-or-create under MainMenu; sized to the canvas)
        var main = Find(scene, "MainMenu");
        if (main)
        {
            var logoGo = Find(scene, "Logo");
            if (!logoGo)
            {
                logoGo = new GameObject(
                    "Logo",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );
                logoGo.transform.SetParent(main.transform, false);
            }

            var img = logoGo.GetComponent<Image>();
            img.sprite = logo;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget = false;

            // prouder than before, but sized so it sits ABOVE the button stack with a clean gap
            float w = refW * 0.34f;
            float ratio = logo != null ? logo.rect.height / logo.rect.width : 0.5f;

            var rt = logoGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -refH * 0.04f);
            rt.sizeDelta = new Vector2(w, w * ratio); // keep the logo's own aspect
            logoGo.transform.SetAsLastSibling();

            // gentle float so the logo feels alive
            if (logoGo.GetComponent<FloatBob>() == null)
                logoGo.AddComponent<FloatBob>();
        }

        WireOptions(scene, refW, refH, btn);
        WirePlayMenu(scene, refW, refH, btn);

        // Smooth fade-in when switching between menu sub-screens
        foreach (var p in new[] { "MainMenu", "OptionsMenu", "PlayMenu" })
        {
            var go = Find(scene, p);
            if (!go)
                continue;
            if (!go.GetComponent<CanvasGroup>())
                go.AddComponent<CanvasGroup>();
            if (!go.GetComponent<FadeInOnEnable>())
                go.AddComponent<FadeInOnEnable>();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // Level-select screen: fix its own Back button (reset any prior bad skin, put it top-left).
    // The 1–9 level buttons are square and need their own sprite — handled in a later pass.
    static void WirePlayMenu(Scene scene, float refW, float refH, Sprite btn)
    {
        var play = Find(scene, "PlayMenu");
        if (!play)
            return;

        // Reskin the 1–9 level buttons as rounded pink candy buttons with rose numbers
        var grid = Find(scene, "World1LevelButtons");
        if (grid)
        {
            var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            var cream = new Color(1f, 0.96f, 0.92f); // soft cream tile — matches the kit cards
            var rose = TextPlum; // wine-rose number, same as the headings
            var levelButtons = grid.GetComponentsInChildren<Button>(true);
            for (int li = 0; li < levelButtons.Length; li++)
            {
                var b = levelButtons[li];
                var img = (b.targetGraphic as Image) ?? b.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = rounded;
                    img.type = Image.Type.Sliced;
                    img.color = cream;
                    img.pixelsPerUnitMultiplier = 0.3f; // generous corner radius → soft, modern, rounder
                    // soft coral edge so the cream tile reads on the pink background
                    var ol = b.GetComponent<Outline>() ?? b.gameObject.AddComponent<Outline>();
                    ol.effectColor = new Color(1f, 0.6f, 0.7f, 0.5f);
                    ol.effectDistance = new Vector2(3f, -3f);
                    b.transition = Selectable.Transition.ColorTint;
                    var cb = b.colors;
                    cb.normalColor = Color.white;
                    cb.highlightedColor = new Color(1f, 0.92f, 0.95f);
                    cb.pressedColor = new Color(0.9f, 0.78f, 0.84f);
                    cb.disabledColor = new Color(0.86f, 0.83f, 0.85f, 1f); // locked: clean soft grey, opaque (no muddy pink bleed)
                    cb.fadeDuration = 0.08f;
                    b.colors = cb;
                }
                foreach (var t in b.GetComponentsInChildren<Text>(true))
                    t.color = rose;
                foreach (var t in b.GetComponentsInChildren<TMP_Text>(true))
                {
                    t.color = rose;
                    t.enableVertexGradient = false; // kill the harsh red gradient on the numbers
                    if (t.font != null)
                        t.fontSharedMaterial = t.font.material;
                }
                if (b.GetComponent<ButtonBounce>() == null)
                    b.gameObject.AddComponent<ButtonBounce>(); // hover-bounce on level tiles too

                AddLevelStars(b.gameObject, li + 1); // saved rating row (Level01 = build index 1)
            }
        }
        else
        {
            Debug.LogWarning("[ArtRevamp] 'World1LevelButtons' not found.");
        }

        var backT = FindIn(play.transform, "BackButoon");
        if (backT)
        {
            RemoveChild(backT.gameObject, "Capsule"); // clear any prior stray skin
            var brt = backT.GetComponent<RectTransform>();
            brt.localScale = Vector3.one;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f); // top-left
            brt.pivot = new Vector2(0f, 1f);
            brt.sizeDelta = new Vector2(refW * 0.18f, refH * 0.11f);
            brt.anchoredPosition = new Vector2(refW * 0.03f, -refH * 0.03f);
            SkinButton(backT.gameObject, btn, false);
        }
    }

    // Add a small 3-star rating row to a level-select button. LevelStars lights up the saved rating
    // (from SaveSystem) at runtime and hides the row while the level is locked.
    static void AddLevelStars(GameObject button, int buildIndex)
    {
        RemoveChild(button, "LevelStars");
        var row = new GameObject("LevelStars", typeof(RectTransform), typeof(LevelStars));
        row.transform.SetParent(button.transform, false);
        var rrt = row.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0f); // bottom-centre of the tile
        rrt.pivot = new Vector2(0.5f, 0f);
        rrt.sizeDelta = new Vector2(70f, 22f);
        rrt.anchoredPosition = new Vector2(0f, 7f);

        var starSprite = Load("star_3d.png") ?? Load("icon_star.png");
        const float sz = 18f,
            gap = 4f;
        var imgs = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var s = MakeImage(row.transform, "Star" + i, starSprite, Color.white);
            var srt = s.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(sz, sz);
            srt.anchoredPosition = new Vector2((i - 1) * (sz + gap), 0f);
            var im = s.GetComponent<Image>();
            im.preserveAspect = true;
            im.raycastTarget = false;
            imgs[i] = im;
        }

        var ls = row.GetComponent<LevelStars>();
        ls.buildIndex = buildIndex;
        ls.stars = imgs;
        ls.earnedColor = Color.white;
        ls.emptyColor = new Color(0.55f, 0.5f, 0.55f, 0.6f);
        row.transform.SetAsLastSibling(); // above the tile number
    }

    // Reskin the Options panel: card background, pink sliders, plum text, Back button.
    static void WireOptions(Scene scene, float refW, float refH, Sprite btn)
    {
        var options = Find(scene, "OptionsMenu");
        if (!options)
        {
            Debug.LogWarning("[ArtRevamp] 'OptionsMenu' not found.");
            return;
        }

        // ── REBUILT FROM SCRATCH (idempotent) ── A clean card + a FRESH title. The FUNCTIONAL volume
        // sliders + Back are positioned over the card's bands but kept parented to OptionsMenu (NOT the
        // card), so re-running never destroys them. (The earlier version reparented them INTO the card;
        // the next card-rebuild then deleted them — that's how the original title object got lost.)
        var cardSprite = CardSprite();
        float cw = refW * 0.6f;
        float ch = refH * 0.84f;

        var oShadow = MakeImage(options.transform, "CardShadow", cardSprite, CardShadowSoft);
        var oshrt = oShadow.GetComponent<RectTransform>();
        oshrt.anchorMin = oshrt.anchorMax = new Vector2(0.5f, 0.5f);
        oshrt.pivot = new Vector2(0.5f, 0.5f);
        oshrt.sizeDelta = new Vector2(cw + 4f, ch + 4f);
        oshrt.anchoredPosition = new Vector2(0f, -refH * 0.018f);
        oShadow.transform.SetAsFirstSibling();

        var card = MakeImage(options.transform, "Card", cardSprite, Color.white);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(cw, ch);
        crt.anchoredPosition = Vector2.zero;
        card.transform.SetSiblingIndex(oShadow.transform.GetSiblingIndex() + 1);

        // Title — created FRESH each run (the original scene title is gone; we no longer depend on it).
        // It lives under the card (recreated each run, so destroying the card with it is harmless).
        RemoveChild(card, "OptionsTitle");
        var titleGo = new GameObject("OptionsTitle", typeof(RectTransform));
        titleGo.transform.SetParent(card.transform, false);
        var ttmp = titleGo.AddComponent<TextMeshProUGUI>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/_Project/Fonts/Roboto-Medium SDF.asset"
        );
        if (font != null)
            ttmp.font = font;
        ttmp.text = "OPTIONS";
        ttmp.color = TextPlum;
        ttmp.fontStyle = FontStyles.Bold;
        ttmp.alignment = TextAlignmentOptions.Center;
        ttmp.enableWordWrapping = false;
        ttmp.enableAutoSizing = true;
        ttmp.fontSizeMin = 8;
        ttmp.fontSizeMax = 70;
        ttmp.raycastTarget = false;
        var trt = titleGo.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.1f, 0.72f);
        trt.anchorMax = new Vector2(0.9f, 0.9f);
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        // Functional Music/SFX rows are laid out below by the SHARED LayoutOptionRows (same as pause).
        // Back button — REBUILT fresh under the card (the original Options Back object was destroyed
        // in an earlier pass) and wired to return to the main menu via the same SetActive panel toggle
        // the rest of the menu uses.
        RemoveChild(card, "OptionsBack");
        var backGo = new GameObject(
            "OptionsBack",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );
        backGo.transform.SetParent(card.transform, false);
        var bRt = backGo.GetComponent<RectTransform>();
        // FIXED size (not stretch) so SkinButton reads a real height → proper rounded pill + sized text
        bRt.anchorMin = bRt.anchorMax = new Vector2(0.5f, 0.5f);
        bRt.pivot = new Vector2(0.5f, 0.5f);
        bRt.sizeDelta = new Vector2(cw * 0.44f, ch * 0.13f);
        bRt.anchoredPosition = new Vector2(0f, -ch * 0.34f); // bottom block (matches pause Options)
        var bLabel = new GameObject("Text", typeof(RectTransform));
        bLabel.transform.SetParent(backGo.transform, false);
        var bTmp = bLabel.AddComponent<TextMeshProUGUI>();
        if (font != null)
            bTmp.font = font;
        bTmp.text = "Back";
        bTmp.alignment = TextAlignmentOptions.Center;
        var blRt = bLabel.GetComponent<RectTransform>();
        blRt.anchorMin = Vector2.zero;
        blRt.anchorMax = Vector2.one;
        blRt.offsetMin = Vector2.zero;
        blRt.offsetMax = Vector2.zero;
        var mainMenu = Find(scene, "MainMenu");
        var bBtn = backGo.GetComponent<Button>();
        for (int i = bBtn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(bBtn.onClick, i);
        UnityEventTools.AddBoolPersistentListener(bBtn.onClick, options.SetActive, false);
        if (mainMenu != null)
            UnityEventTools.AddBoolPersistentListener(bBtn.onClick, mainMenu.SetActive, true);

        // ── Styling ── group labels → deep plum, no gradient/shadow (title already styled above).
        foreach (var t in options.GetComponentsInChildren<Text>(true))
            t.color = TextPlum;
        foreach (var t in options.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == ttmp)
                continue;
            t.color = TextPlum;
            t.enableVertexGradient = false;
            if (t.font != null)
                t.fontSharedMaterial = t.font.material;
        }
        foreach (var sh in options.GetComponentsInChildren<Shadow>(true))
            Object.DestroyImmediate(sh);

        // Music + SFX rows — same shared layout as the pause Options, so the two screens are identical.
        LayoutOptionRows(options.transform, cw, ch, refH);

        // back → the accent pill (its white label is set inside StyleAccentButton, after the plum pass)
        if (backGo)
            StyleAccentButton(backGo);
    }

    // Candy-style every Slider under `root`: a slim fully-rounded "pill" track + accent fill + a clear
    // round white knob that sits proud of the track. Shared by the menu Options and the pause Options.
    static void StyleSliders(Transform root, float refH)
    {
        var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); // WHITE circle — contrasts the coral fill
        float trackH = refH * 0.034f; // substantial, rounded — not a thin progress bar
        foreach (var sl in root.GetComponentsInChildren<Slider>(true))
        {
            if (
                sl.transform.Find("Background") is RectTransform bgr
                && bgr.TryGetComponent(out Image bgi)
            )
            {
                bgi.sprite = rounded;
                bgi.type = Image.Type.Sliced;
                bgi.color = new Color(0.9f, 0.85f, 0.88f); // soft muted track
                bgi.pixelsPerUnitMultiplier = 0.6f; // big radius → pill
                bgr.anchorMin = new Vector2(0f, 0.5f);
                bgr.anchorMax = new Vector2(1f, 0.5f);
                bgr.pivot = new Vector2(0.5f, 0.5f);
                bgr.offsetMin = new Vector2(0f, -trackH / 2f);
                bgr.offsetMax = new Vector2(0f, trackH / 2f);
            }
            if (sl.transform.Find("Fill Area") is RectTransform fillArea)
            {
                fillArea.anchorMin = new Vector2(0f, 0.5f);
                fillArea.anchorMax = new Vector2(1f, 0.5f);
                fillArea.pivot = new Vector2(0.5f, 0.5f);
                fillArea.offsetMin = new Vector2(6f, -trackH / 2f);
                fillArea.offsetMax = new Vector2(-6f, trackH / 2f);
            }
            if (sl.fillRect && sl.fillRect.TryGetComponent(out Image fi))
            {
                fi.sprite = rounded;
                fi.type = Image.Type.Sliced;
                fi.color = Accent;
                fi.pixelsPerUnitMultiplier = 0.6f;
            }
            if (sl.handleRect && sl.handleRect.TryGetComponent(out Image hi))
            {
                hi.sprite = knob;
                hi.color = Color.white;
                hi.preserveAspect = true;
                sl.handleRect.sizeDelta = new Vector2(refH * 0.072f, refH * 0.072f); // clearly proud of the track
            }
        }
    }

    // Build the pause Options sub-panel look: a centred cream card (+ soft shadow + dim) behind the
    // existing OptionsText / Music+SFX sliders / Back, all restyled to the Soft Candy kit. Functional
    // elements stay parented to the group (never reparented into the card) so re-runs can't destroy them.
    static void StylePauseOptions(Transform group, TMP_FontAsset font)
    {
        const float refW = 800f,
            refH = 600f; // Game Canvas reference resolution (matches the Menu scene → identical look)
        float cw = refW * 0.6f; // same card proportions as the menu-scene Options (WireOptions)
        float ch = refH * 0.84f;
        var cardSprite = CardSprite();

        // dim backdrop so it reads as a modal over the gameplay
        var dim = MakeImage(group, "Dim", null, new Color(0f, 0f, 0f, 0.45f));
        var drt = dim.GetComponent<RectTransform>();
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = Vector2.zero;
        drt.offsetMax = Vector2.zero;
        dim.transform.SetAsFirstSibling();

        var shadow = MakeImage(group, "OptShadow", cardSprite, CardShadowSoft);
        var shrt = shadow.GetComponent<RectTransform>();
        shrt.anchorMin = shrt.anchorMax = new Vector2(0.5f, 0.5f);
        shrt.pivot = new Vector2(0.5f, 0.5f);
        shrt.sizeDelta = new Vector2(cw + 4f, ch + 4f);
        shrt.anchoredPosition = new Vector2(0f, -refH * 0.018f);
        shadow.transform.SetSiblingIndex(1); // above the dim, below the card

        var card = MakeImage(group, "OptCard", cardSprite, Color.white);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(cw, ch);
        crt.anchoredPosition = Vector2.zero;
        card.transform.SetSiblingIndex(2);

        // Title (OptionsText) — strip the dark-red gradient/underlay, restyle plum, seat at card top.
        // Title — created FRESH (restyling the original OptionsText rendered inconsistently → it went
        // invisible). Hide the old one and build a guaranteed "OPTIONS" title, exactly like WireOptions.
        var oldTitle = FindIn(group, "OptionsText");
        if (oldTitle != null)
            oldTitle.gameObject.SetActive(false);
        RemoveChild(group.gameObject, "OptionsTitle");
        var titleGo = new GameObject("OptionsTitle", typeof(RectTransform));
        titleGo.transform.SetParent(group, false);
        var ttmp = titleGo.AddComponent<TextMeshProUGUI>();
        if (font != null)
            ttmp.font = font;
        ttmp.text = "OPTIONS";
        ttmp.color = TextPlum;
        ttmp.fontStyle = FontStyles.Bold;
        ttmp.alignment = TextAlignmentOptions.Center;
        ttmp.enableWordWrapping = false;
        ttmp.enableAutoSizing = true;
        ttmp.fontSizeMin = 8f;
        ttmp.fontSizeMax = 70f;
        ttmp.raycastTarget = false;
        var trt = titleGo.GetComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.pivot = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = new Vector2(cw * 0.8f, ch * 0.18f);
        trt.anchoredPosition = new Vector2(0f, ch * 0.34f); // top block
        titleGo.transform.SetAsLastSibling();

        // Middle block: Music + SFX rows, laid out by the shared routine (same as the menu Options).
        LayoutOptionRows(group, cw, ch, refH);

        // group labels (MUSIC / SFX) → plum, no leftover gradient/underlay. Button labels are left for
        // the re-whiten pass in ApplyGameUI.
        foreach (var t in group.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == ttmp || t.GetComponentInParent<Button>() != null)
                continue; // leave the fresh title + button labels alone
            t.color = TextPlum;
            t.enableVertexGradient = false;
            if (t.font != null)
                t.fontSharedMaterial = t.font.material;
        }
        foreach (var t in group.GetComponentsInChildren<Text>(true))
            if (t.GetComponentInParent<Button>() == null)
                t.color = TextPlum;

        // Back button → coral candy pill (keeps its existing onClick).
        var back = FindIn(group, "BackButoon");
        if (back != null)
        {
            var brt = back.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(cw * 0.44f, ch * 0.13f);
            brt.anchoredPosition = new Vector2(0f, -ch * 0.34f); // bottom block (symmetric with title)
            SkinButton(back.gameObject, null, false);
            back.SetAsLastSibling();
        }
    }

    // Shared Music + SFX row layout for BOTH options panels (menu scene + in-game pause) so they look
    // identical. The groups are plain Transforms (no RectTransform) → position via localPosition; each
    // row is then a centred flex-col (label above a centred full-width slider).
    static void LayoutOptionRows(Transform panelRoot, float cw, float ch, float refH)
    {
        var music = FindIn(panelRoot, "MusicGroup");
        var sfx = FindIn(panelRoot, "SFXGroup");
        if (music != null)
            music.localPosition = new Vector3(0f, ch * 0.08f, 0f); // upper row (tighter gap)
        if (sfx != null)
            sfx.localPosition = new Vector3(0f, -ch * 0.08f, 0f); // lower row
        StyleSliders(panelRoot, refH);
        float bandW = cw * 0.72f,
            bandH = ch * 0.12f;
        CenterOptionRow(music, bandW, bandH);
        CenterOptionRow(sfx, bandW, bandH);
    }

    // Inside a Music/SFX group: centre the label above a full-width centred slider, so the row reads
    // centred instead of left-weighted.
    static void CenterOptionRow(Transform grp, float bandW, float bandH)
    {
        if (grp == null)
            return;
        foreach (Transform child in grp)
        {
            if (!(child is RectTransform rt))
                continue;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            if (child.GetComponent<Slider>() != null)
            {
                rt.sizeDelta = new Vector2(bandW, bandH * 0.42f);
                rt.anchoredPosition = new Vector2(0f, -bandH * 0.22f); // slider below the label
            }
            else
            {
                rt.sizeDelta = new Vector2(bandW, bandH * 0.5f);
                rt.anchoredPosition = new Vector2(0f, bandH * 0.26f); // label above, centred
                if (child.TryGetComponent(out TMP_Text tt))
                    tt.alignment = TextAlignmentOptions.Center;
                if (child.TryGetComponent(out Text lt))
                    lt.alignment = TextAnchor.MiddleCenter;
            }
        }
    }

    // Put a round icon sprite on a button's Icon image: centred + square + Simple + preserveAspect, so
    // it can never get squeezed into an oval (Sliced / stretched rects were doing that).
    static void SetRoundIcon(Image ico, string iconName, float size)
    {
        var sp = Load(iconName);
        if (sp != null)
            ico.sprite = sp;
        ico.type = Image.Type.Simple;
        ico.preserveAspect = true;
        var rt = ico.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    // Shared colour transition for the round HUD/win buttons. selectedColor = normal + Navigation None
    // so the button never lights up just from being auto-selected (e.g. when the win screen appears).
    static void StyleHudButtonColors(Button b)
    {
        b.transition = Selectable.Transition.ColorTint;
        var cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.9f, 0.94f);
        cb.pressedColor = new Color(0.9f, 0.78f, 0.84f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    // Make a fresh sliced Image child (used to build cards/shadows from scratch).
    static GameObject MakeImage(Transform parent, string name, Sprite sprite, Color color)
    {
        RemoveChild(parent.gameObject, name);
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return go;
    }

    // Reparent an existing element under a card and stretch it to fill a band given as fractions of
    // the card (x0,y0)-(x1,y1). Deterministic layout — no dependence on the element's old anchors.
    // Position an existing element OVER a centred card's band (fractions of the card) WITHOUT
    // reparenting it into the card — it stays under `parent` (OptionsMenu, full-screen + centred) so a
    // card rebuild can never destroy it. Rendered above the card via SetAsLastSibling.
    static void PlaceAtBand(
        GameObject go,
        Transform parent,
        float cw,
        float ch,
        float x0,
        float y0,
        float x1,
        float y1
    )
    {
        if (go == null)
            return;
        var rt = go.GetComponent<RectTransform>();
        if (rt == null)
            return;
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one;
        go.transform.localRotation = Quaternion.identity;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2((x1 - x0) * cw, (y1 - y0) * ch);
        rt.anchoredPosition = new Vector2(
            ((x0 + x1) * 0.5f - 0.5f) * cw,
            ((y0 + y1) * 0.5f - 0.5f) * ch
        );
        go.transform.SetAsLastSibling(); // render above the card
    }

    // Options "Back" reuses the one shared button styler (coral pill + white label).
    static void StyleAccentButton(GameObject go) => SkinButton(go, null, false);

    // Add (or reuse) a sliced capsule background behind a text-only button,
    // and soften the label color so it reads on the pink capsule.
    static void SkinButton(GameObject go, Sprite capsule, bool hasIcon)
    {
        float bH = go.GetComponent<RectTransform>().sizeDelta.y;
        float side = bH * 0.45f;
        float leftPad = hasIcon ? bH * 1.25f : side; // clear the left icon
        float vert = bH * 0.22f;

        var bgT = go.transform.Find("Capsule");
        Image img;
        if (bgT == null)
        {
            var bgGo = new GameObject(
                "Capsule",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            bgGo.transform.SetParent(go.transform, false);
            bgGo.transform.SetAsFirstSibling(); // render behind the label
            img = bgGo.GetComponent<Image>();
            var rt = bgGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; // fill the button
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            img = bgT.GetComponent<Image>();
        }

        img.sprite = ButtonSprite(); // the designed coral pill (ui_btn) — ignore the passed sprite
        img.type = Image.Type.Sliced;
        img.color = Color.white; // sprite IS the colour
        // keep the pill's rounded ends circular at any button height (ui_btn end radius ≈ 85px native)
        img.pixelsPerUnitMultiplier = Mathf.Max(1f, 170f / Mathf.Max(1f, bH));

        RemoveChild(go, "Lip"); // legacy: drop any old fake lip

        // Soft drop-shadow so the button lifts off the (pink) background instead of blending in.
        var shT = go.transform.Find("BtnShadow");
        Image shImg;
        if (shT == null)
        {
            var shGo = new GameObject(
                "BtnShadow",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            shGo.transform.SetParent(go.transform, false);
            shImg = shGo.GetComponent<Image>();
            var shrt = shGo.GetComponent<RectTransform>();
            shrt.anchorMin = Vector2.zero;
            shrt.anchorMax = Vector2.one;
        }
        else
        {
            shImg = shT.GetComponent<Image>();
        }
        shImg.sprite = img.sprite;
        shImg.type = Image.Type.Sliced;
        shImg.color = new Color(0.45f, 0.32f, 0.4f, 0.3f); // soft warm shadow
        shImg.raycastTarget = false;
        shImg.pixelsPerUnitMultiplier = img.pixelsPerUnitMultiplier;
        var shadowRt = shImg.rectTransform;
        shadowRt.offsetMin = new Vector2(0f, -bH * 0.12f); // nudge down
        shadowRt.offsetMax = new Vector2(0f, -bH * 0.12f);
        shImg.transform.SetAsFirstSibling(); // behind the capsule + label

        // hide the button's own graphic (e.g. an old black background) behind the capsule
        if (go.TryGetComponent(out Image ownImg) && ownImg != img && ownImg != shImg)
            ownImg.enabled = false;

        if (go.TryGetComponent(out Button b))
        {
            b.targetGraphic = img;
            b.transition = Selectable.Transition.ColorTint; // kill old sprite-swap ghost
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 0.90f, 0.94f); // gentle pink brighten
            cb.pressedColor = new Color(0.90f, 0.78f, 0.84f); // slight press darken
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
        }

        var soft = Color.white; // white label reads cleanly on the coral pill
        var legacy = go.GetComponentInChildren<Text>(true);
        if (legacy)
        {
            legacy.color = soft;
            legacy.alignment = TextAnchor.MiddleCenter;
            legacy.resizeTextForBestFit = true;
            legacy.resizeTextMinSize = 8;
            legacy.resizeTextMaxSize = 30;
            FitLabel(legacy.rectTransform, leftPad, side, vert);
        }
        var tmp = go.GetComponentInChildren<TMP_Text>(true);
        if (tmp)
        {
            tmp.color = soft;
            tmp.enableVertexGradient = false; // kill the orange gradient that was overriding the colour
            if (tmp.font != null)
                tmp.fontSharedMaterial = tmp.font.material; // strip underlay/glow so white shows clean
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 8;
            tmp.fontSizeMax = 30;
            FitLabel(tmp.rectTransform, leftPad, side, vert);
        }

        // Juice: hover-bounce + press-dip on every button (makes the UI feel alive).
        if (go.GetComponent<ButtonBounce>() == null)
            go.AddComponent<ButtonBounce>();
    }

    // Destroy a named child if it exists (used to clear icons from earlier runs).
    static void RemoveChild(GameObject go, string name)
    {
        var t = go.transform.Find(name);
        if (t)
            Object.DestroyImmediate(t.gameObject);
    }

    // Add (or reuse) an icon at the left side of a button, scaled to the button.
    static void AddIcon(GameObject go, Sprite icon)
    {
        if (icon == null)
            return;
        float bH = go.GetComponent<RectTransform>().sizeDelta.y;
        var t = go.transform.Find("Icon");
        Image img;
        if (t == null)
        {
            var iconGo = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            iconGo.transform.SetParent(go.transform, false);
            img = iconGo.GetComponent<Image>();
        }
        else
        {
            img = t.GetComponent<Image>();
        }
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); // left-center
        rt.pivot = new Vector2(0f, 0.5f);
        float s = bH * 0.55f;
        rt.sizeDelta = new Vector2(s, s);
        rt.anchoredPosition = new Vector2(bH * 0.4f, 0f);
        img.sprite = icon;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.transform.SetAsLastSibling(); // render above the capsule
    }

    // Inset the label inside the button so the text keeps margins from the capsule edge.
    static void FitLabel(RectTransform rt, float left, float right, float vert)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, vert);
        rt.offsetMax = new Vector2(-right, -vert);
    }

    // Recursive find by name, including inactive objects.
    static GameObject Find(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var hit = FindIn(root.transform, name);
            if (hit)
                return hit.gameObject;
        }
        return null;
    }

    static Transform FindIn(Transform t, string name)
    {
        if (t.name == name)
            return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var hit = FindIn(t.GetChild(i), name);
            if (hit)
                return hit;
        }
        return null;
    }
}
#endif
