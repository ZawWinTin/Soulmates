using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// The garden design system: one responsive layout and navigation owner for every scene.
/// Art is serialized through GardenTheme; gameplay and the web save contract stay independent.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class GardenInterface : MonoBehaviour
{
    public GardenTheme theme;
    public static GardenInterface Active { get; private set; }
    public bool TouchControls => GameBridge.IsTouch || Application.isMobilePlatform || forceTouch;
    public bool forceTouch;
    bool retrying;
    static readonly Color Paper = Hex("FFF4ED"),
        Ink = Hex("69465F"),
        Muted = Hex("88637D"),
        Sage = Hex("DDF3E5"),
        Green = Hex("F48F9C"),
        Blue = Hex("BCE6F9"),
        Rose = Hex("FFD0E3"),
        Gold = Hex("FFD16B"),
        Line = Hex("DFCFDF");
    static readonly string[] Names =
    {
        "First steps",
        "A little closer",
        "Crossing paths",
        "Side by side",
        "The long way",
        "Meet me halfway",
        "Leap of faith",
        "Almost home",
        "Together, at last",
    };
    Canvas canvas;
    RectTransform root,
        screen,
        modal;
    CanvasGroup fade;
    GameController game;
    PlayerController boy,
        girl;
    Camera boardCamera;
    Bounds boardBounds;
    bool hasBoard,
        completed,
        navigating;
    int width,
        height,
        lastUnlocked;
    bool lastTouch;
    string page,
        overlay;
    float w,
        h,
        modalReturnScale;
    TMP_Text clockLabel,
        goalLabel;
    Image timeStarIcon,
        bonusStarIcon;
    int sceneIndex;
    bool Gameplay => sceneIndex >= 1 && sceneIndex <= 9;
    bool Portrait => h > w;
    float Margin => TouchControls ? 24f : 54f;

    void Awake()
    {
        Active = this;
        sceneIndex = SceneManager.GetActiveScene().buildIndex;
        Time.timeScale = 1;
        PauseMenu.isGamePaused = false;
        if (EventSystem.current == null)
            new GameObject(
                "Garden Event System",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
        var go = new GameObject(
            "Garden Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        root = go.GetComponent<RectTransform>();
        page =
            Gameplay ? "game"
            : sceneIndex == 10 ? "credits"
            : journeyOnLoad ? "journey"
            : "home";
        journeyOnLoad = false;
    }

    void OnEnable()
    {
        GameBridge.DeviceChanged += DeviceChanged;
    }

    void OnDisable()
    {
        GameBridge.DeviceChanged -= DeviceChanged;
        Time.timeScale = 1;
        PauseMenu.isGamePaused = false;
        if (Active == this)
            Active = null;
    }

    void Start()
    {
        if (Gameplay)
        {
            game = FindObjectOfType<GameController>();
            boy = GameObject.FindGameObjectWithTag("Player1").GetComponent<PlayerController>();
            girl = GameObject.FindGameObjectWithTag("Player2").GetComponent<PlayerController>();
            boardCamera = Camera.main;
            var map = GameObject.FindGameObjectWithTag("GroundTileMap").GetComponent<Tilemap>();
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                if (!map.HasTile(cell))
                    continue;
                var p = map.GetCellCenterWorld(cell);
                if (!hasBoard)
                {
                    boardBounds = new Bounds(p, Vector3.zero);
                    hasBoard = true;
                }
                else
                    boardBounds.Encapsulate(p);
            }
        }
        Rebuild();
    }

    void DeviceChanged()
    {
        if (screen != null)
            Rebuild();
    }

    void Update()
    {
        if (screen == null)
            return;
        if (width != Screen.width || height != Screen.height || lastTouch != TouchControls)
            Rebuild();
        if (
            Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame
            && !navigating
        )
        {
            if (overlay != null)
                CloseOverlay();
            else if (Gameplay && !completed)
                OpenOverlay("pause");
            else if (!Gameplay)
                ShowPage("home");
        }
        if (Gameplay && game != null)
        {
            if (clockLabel != null)
            {
                float remaining = Mathf.Max(0, game.timeParSeconds - Time.timeSinceLevelLoad);
                bool earnedTime = game.isLevelCompleted ? game.timeStarEarned : remaining > 0;
                clockLabel.text = earnedTime
                    ? (
                        game.isLevelCompleted
                            ? "Earned!"
                            : $"{Mathf.CeilToInt(remaining) / 60:00}:{Mathf.CeilToInt(remaining) % 60:00}"
                    )
                    : "Keep going";
                clockLabel.fontSize = w <= 720 ? (earnedTime ? 15 : 10) : (earnedTime ? 13 : 10);
                if (timeStarIcon != null)
                    timeStarIcon.color = earnedTime
                        ? Color.white
                        : new Color(.7f, .65f, .72f, .45f);
                if (bonusStarIcon != null)
                    bonusStarIcon.color = game.starCollected
                        ? Color.white
                        : new Color(1, 1, 1, .4f);
                if (goalLabel != null)
                    goalLabel.text = game.starCollected ? "Found!" : "Find star";
            }
            if (game.ResultsReady && !completed)
            {
                completed = true;
                OpenOverlay("win");
            }
        }
    }

    public void RefreshProgress()
    {
        if (!Gameplay && screen != null)
            Rebuild();
    }

    public void ShowPage(string next)
    {
        page = next;
        Rebuild();
    }

    public void PreviewOverlay(string kind)
    {
        OpenOverlay(kind);
    }

    public void SetTouchPreview(bool enabled)
    {
        forceTouch = enabled;
        Rebuild();
    }

    void Rebuild()
    {
        width = Screen.width;
        height = Screen.height;
        lastTouch = TouchControls;
        lastUnlocked = Unlocked();
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        bool portrait = height > width;
        h = TouchControls ? (portrait ? 820 : 480) : 720;
        w = h * width / Mathf.Max(1f, height);
        scaler.referenceResolution = new Vector2(w, h);
        scaler.matchWidthOrHeight = 1;
        if (screen != null)
        {
            screen.gameObject.SetActive(false);
            Destroy(screen.gameObject);
        }
        screen = Rect("Screen", root, 0, 0, w, h);
        // Safe-area coordinates are normalized to the canvas backing resolution.
        Rect safe = Screen.safeArea;
        float sx = safe.x / Mathf.Max(1, width) * w,
            sy = (height - safe.yMax) / Mathf.Max(1, height) * h;
        if (safe.width > 0 && safe.height > 0)
        {
            w = safe.width / width * w;
            h = safe.height / height * h;
            screen.anchoredPosition = new Vector2(sx, -sy);
            screen.sizeDelta = new Vector2(w, h);
        }
        clockLabel = null;
        goalLabel = null;
        timeStarIcon = bonusStarIcon = null;
        if (Gameplay)
            BuildGame();
        else
        {
            Box(screen, "Paper", 0, 0, w, h, Paper, false);
            float skyW = Mathf.Max(w, h * 16f / 9f);
            Picture(screen, theme.illustration, (w - skyW) / 2, 0, skyW, h).preserveAspect = false;
            if (page == "home")
                BuildHome();
            else if (page == "journey")
                BuildJourney();
            else
                BuildCredits();
        }
        if (retrying)
            BuildRetryNotice();
        modal = null;
        if (overlay != null)
            BuildOverlay();
        // A small unscaled entrance; reduced-motion preference removes it completely.
        if (PlayerPrefs.GetInt("GardenReducedMotion", 0) == 0 && !Gameplay)
        {
            fade = screen.gameObject.AddComponent<CanvasGroup>();
            StartCoroutine(Reveal(fade));
        }
    }

    IEnumerator Reveal(CanvasGroup group)
    {
        float t = 0;
        while (t < .18f && group != null)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / .18f);
            yield return null;
        }
        if (group != null)
            group.alpha = 1;
    }

    void BuildHome()
    {
        bool landscape = TouchControls && !Portrait;
        float logoW = landscape ? w * .47f : Mathf.Min(w * .94f, 560);
        float logoH = landscape ? h * .67f : h * .43f;
        float logoX = landscape ? w * .025f : (w - logoW) / 2;
        Picture(screen, theme.logo, logoX, h * .035f, logoW, logoH);
        float bw = Mathf.Min(280, w - 68);
        float bx = landscape ? w * .72f - bw / 2 : (w - bw) / 2;
        float by = landscape ? 100 : h * .49f;
        Text(
            screen,
            "Little hops. Big love!",
            landscape ? logoX : 20,
            landscape ? h * .72f : h * .435f,
            landscape ? logoW : w - 40,
            30,
            20,
            Ink,
            true,
            TextAlignmentOptions.Center
        );
        Button(
            screen,
            Unlocked() > 1 ? "Let's play!" : "Play!",
            bx,
            by,
            bw,
            64,
            () => Navigate(Mathf.Min(Unlocked(), 9)),
            Green,
            Ink
        );
        Button(
            screen,
            "Choose a level",
            bx + 15,
            by + 78,
            bw - 30,
            54,
            () => ShowPage("journey"),
            Rose,
            Ink
        );
        Button(
            screen,
            "How to play",
            bx + 15,
            by + 144,
            bw - 30,
            54,
            () => OpenOverlay("help"),
            Sage,
            Ink
        );
        Footer();
    }

    void Footer()
    {
        float m = Margin;
        Button(screen, "Settings", m, h - 60, 90, 44, () => OpenOverlay("settings"), Paper, Muted);
        Button(screen, "Credits", m + 95, h - 60, 80, 44, () => ShowPage("credits"), Paper, Muted);
        Button(screen, "Exit", m + 180, h - 60, 60, 44, () => OpenOverlay("exit"), Paper, Muted);
        if (!Portrait)
            Text(
                screen,
                "9 small adventures",
                w - m - 170,
                h - 46,
                170,
                24,
                12,
                Muted,
                false,
                TextAlignmentOptions.Right
            );
    }

    void Header(string eyebrow, string title, string subtitle)
    {
        float m = Margin;
        Button(
            screen,
            "Back",
            m,
            22,
            100,
            40,
            () =>
            {
                if (sceneIndex == 10)
                    Navigate(0);
                else
                    ShowPage("home");
            },
            Sage,
            Ink,
            ButtonIcon.Back
        );
        bool shortScreen = TouchControls && !Portrait;
        Text(screen, eyebrow, m, shortScreen ? 72 : 85, w - 2 * m, 22, 12, Muted, true);
        Text(
            screen,
            title,
            m,
            shortScreen ? 98 : 111,
            w - 2 * m,
            60,
            TouchControls ? 36 : 48,
            Ink,
            true
        );
        Text(
            screen,
            subtitle,
            m,
            shortScreen ? 151 : 176,
            w - 2 * m,
            38,
            TouchControls ? 14 : 18,
            Muted
        );
    }

    void BuildJourney()
    {
        lastUnlocked = Unlocked();
        Button(screen, "Back", 20, 22, 90, 42, () => ShowPage("home"), Sage, Ink);
        Text(
            screen,
            "Pick a little adventure!",
            20,
            82,
            w - 40,
            55,
            Portrait ? 28 : 36,
            Ink,
            true,
            TextAlignmentOptions.Center
        );
        bool shortScreen = TouchControls && !Portrait;
        float top = shortScreen ? 157 : 190;
        float areaW = Mathf.Min(w - 42, 740),
            left = (w - areaW) / 2;
        float stepY = (h - top - 50) / 3;
        float tileW = Mathf.Min(areaW / 3 - 14, shortScreen ? 114 : 155);
        float tileH = tileW * theme.ground.rect.height / theme.ground.rect.width;
        Vector2[] centers = new Vector2[9];
        for (int i = 0; i < 9; i++)
        {
            int row = i / 3,
                col = row % 2 == 0 ? i % 3 : 2 - i % 3;
            centers[i] = new Vector2(
                left + areaW * (col + .5f) / 3,
                top + row * stepY + (col == 1 ? -12 : 8)
            );
        }
        for (int i = 1; i < 9; i++)
        for (int d = 1; d <= 5; d++)
        {
            var point = Vector2.Lerp(centers[i - 1], centers[i], d / 6f);
            Box(screen, "Trail pebble", point.x - 3, point.y + 14, 6, 6, Rose);
        }
        for (int i = 1; i <= 9; i++)
        {
            int index = i;
            bool unlocked = i <= lastUnlocked;
            var c = centers[i - 1];
            var island = Rect("Level " + i, screen, c.x - tileW / 2, c.y - 57, tileW, tileH + 80);
            var tile = Picture(island, theme.ground, 0, 45, tileW, tileH);
            tile.color = unlocked ? Color.white : new Color(.8f, .8f, .9f, .65f);
            var b = Button(
                island,
                "",
                0,
                45,
                tileW,
                Mathf.Max(60, tileH),
                () => Navigate(index),
                Color.clear,
                Ink
            );
            b.targetGraphic = tile;
            b.interactable = unlocked;
            float numberSize = TouchControls ? 23 : 28;
            Text(
                island,
                i.ToString(),
                tileW / 2 - 30,
                59,
                60,
                36,
                numberSize,
                new Color(1, .98f, .83f, .9f),
                true,
                TextAlignmentOptions.Center
            );
            Text(
                island,
                i.ToString(),
                tileW / 2 - 30,
                57,
                60,
                36,
                numberSize,
                unlocked ? Ink : Muted,
                true,
                TextAlignmentOptions.Center
            );
            bool current = i == Mathf.Min(lastUnlocked, 9);
            if (current)
                Picture(island, i % 2 == 0 ? theme.girl : theme.boy, tileW / 2 - 23, 0, 46, 52);
            int stars = SaveSystem.GetStars(i);
            for (int k = 0; k < 3; k++)
            {
                var star = Picture(island, theme.star, tileW / 2 - 30 + k * 22, 98, 18, 18);
                star.color = k < stars ? Color.white : new Color(.65f, .6f, .75f, .2f);
            }
            if (!unlocked)
                Text(
                    island,
                    "Soon!",
                    tileW / 2 - 40,
                    123,
                    80,
                    20,
                    11,
                    Muted,
                    true,
                    TextAlignmentOptions.Center
                );
            island.pivot = new Vector2(.5f, .5f);
            island.anchoredPosition += new Vector2(tileW / 2, -(tileH + 80) / 2);
            var feedback = b.gameObject.AddComponent<GardenIslandFeedback>();
            feedback.visual = island;
            feedback.current = current;
        }
    }

    void BuildCredits()
    {
        Header("MADE WITH CARE", "Made with heart.", "Thank you for bringing these two together.");
        float cw = Mathf.Min(w - 2 * Margin, 600),
            x = (w - cw) / 2;
        bool shortScreen = TouchControls && !Portrait;
        float cy = shortScreen ? 195 : 240;
        var card = Box(screen, "Credits", x, cy, cw, Mathf.Min(330, h - cy - 24), Color.white);
        Picture(card, theme.boy, cw / 2 - 85, 18, 80, 80);
        Picture(card, theme.girl, cw / 2 + 5, 18, 80, 80);
        Text(card, "SOULMATES", 25, 112, cw - 50, 28, 15, Muted, true, TextAlignmentOptions.Center);
        Text(
            card,
            "Created by Zaw Win Tin",
            25,
            151,
            cw - 50,
            40,
            TouchControls ? 20 : 25,
            Ink,
            true,
            TextAlignmentOptions.Center
        );
        Text(
            card,
            "For the joy of finding a way, together.",
            25,
            203,
            cw - 50,
            52,
            17,
            Muted,
            false,
            TextAlignmentOptions.Center
        );
    }

    void BuildGame()
    {
        float m = TouchControls ? 16 : 28;
        // Opaque paper bands reserve room for UI, leaving the puzzle clear.
        float top = TouchControls ? 60 : 85;
        float bottom = TouchControls ? (Portrait ? 210 : 180) : 145;
        Box(screen, "Header", 0, 0, w, top, Paper, false);
        Box(screen, "Controls background", 0, h - bottom, w, bottom, Paper, false);
        Text(
            screen,
            $"{sceneIndex:00}  /  09",
            m,
            TouchControls ? 10 : 17,
            110,
            22,
            TouchControls ? 12 : 14,
            Muted,
            true
        );
        Text(
            screen,
            Names[sceneIndex - 1],
            m,
            TouchControls ? 30 : 43,
            TouchControls && Portrait ? w - 2 * m - 110 : w * .34f,
            30,
            TouchControls ? 17 : 23,
            Ink,
            true
        );
        Button(
            screen,
            "Help",
            w - m - 100,
            12,
            44,
            42,
            () => OpenOverlay("help"),
            Sage,
            Ink,
            ButtonIcon.Help
        );
        Button(
            screen,
            "Pause",
            w - m - 46,
            12,
            44,
            42,
            () => OpenOverlay("pause"),
            Green,
            Paper,
            ButtonIcon.Pause
        );
        // Distinct rewards: the countdown earns a time star; the map star is independent.
        bool compact = w <= 720;
        float statusW = compact ? Mathf.Clamp(w - 314, 80, 220) : Mathf.Min(244, w * .29f);
        float statusX = compact ? w / 2 - statusW / 2 : w * .39f;
        float statusY = compact ? h - bottom + 18 : (TouchControls ? 4 : 8);
        var status = Box(
            screen,
            "Star goals",
            statusX,
            statusY,
            statusW,
            compact ? 151 : (TouchControls ? 54 : 67),
            new Color(1, 1, 1, .65f)
        );
        float columnW = compact ? statusW : statusW / 2;
        Text(
            status,
            "Time star",
            0,
            compact ? 7 : 4,
            columnW,
            18,
            compact ? 10 : 11,
            Muted,
            true,
            TextAlignmentOptions.Center
        );
        float meterY = compact ? 28 : 28;
        var track = Box(
            status,
            "Candy meter rim",
            23,
            meterY,
            columnW - 30,
            18,
            new Color(.93f, .87f, .89f)
        );
        var fill = Box(track, "Gold energy", 2, 3, columnW - 34, 12, Gold).GetComponent<Image>();
        var fr = fill.rectTransform;
        fr.anchorMin = new Vector2(0, .5f);
        fr.anchorMax = new Vector2(1, .5f);
        fr.pivot = new Vector2(0, .5f);
        fr.offsetMin = new Vector2(2, -6);
        fr.offsetMax = new Vector2(-2, 6);
        var handle = Box(track, "Sparkle tip", 0, 2, 14, 14, Gold);
        handle.pivot = new Vector2(.5f, .5f);
        Box(handle, "Tip highlight", 4, 4, 6, 6, Color.white);
        if (PlayerPrefs.GetInt("GardenReducedMotion", 0) == 0)
        {
            var sparkle = Rect("Timer sparkles", handle, 7, 7, 0, 0)
                .gameObject.AddComponent<UISparkles>();
            sparkle.sparkleSprite = theme.sparkle;
            sparkle.color = new Color(1, .92f, .55f);
        }
        var badge = Box(status, "Gold star badge", 2, meterY - 10, 38, 38, Gold);
        Box(badge, "Badge cream", 2, 2, 34, 34, Paper);
        timeStarIcon = Picture(badge, theme.star, 5, 5, 28, 28);
        var meter = status.gameObject.AddComponent<LevelTimer>();
        meter.fillBar = fill;
        meter.parStar = timeStarIcon;
        meter.handle = handle;
        clockLabel = Text(
            status,
            "00:00",
            compact ? 0 : 43,
            compact ? 55 : 26,
            compact ? columnW : columnW - 47,
            24,
            compact ? 15 : 13,
            Ink,
            true,
            TextAlignmentOptions.Center
        );
        float bx = compact ? 0 : columnW;
        float by = compact ? 82 : 4;
        Text(
            status,
            "Bonus star",
            bx,
            by,
            columnW,
            18,
            compact ? 10 : 11,
            Muted,
            true,
            TextAlignmentOptions.Center
        );
        bonusStarIcon = Picture(
            status,
            theme.star,
            bx + (compact ? (columnW - 22) / 2 : 12),
            by + 23,
            22,
            22
        );
        goalLabel = Text(
            status,
            "Find star",
            bx + (compact ? 0 : 38),
            by + (compact ? 46 : 22),
            compact ? columnW : columnW - 42,
            21,
            compact ? 10 : 13,
            Muted,
            true,
            TextAlignmentOptions.Center
        );
        if (TouchControls)
        {
            float padW = 131,
                py = h - bottom + 18;
            Pad(16, py, boy, Blue, "BLUE", padW);
            Pad(w - 16 - padW, py, girl, Rose, "ROSE", padW);
            if (w > 720)
                Text(
                    screen,
                    "A path for each.\nA home together.",
                    w / 2 - 110,
                    h - bottom + 55,
                    220,
                    60,
                    15,
                    Muted,
                    false,
                    TextAlignmentOptions.Center
                );
        }
        else
        {
            ControlLesson(screen, m, h - bottom + 7, 190, false, true, 42);
            ControlLesson(screen, w - m - 190, h - bottom + 7, 190, true, true, 42);
            if (w > 720)
                Text(
                    screen,
                    "Little hops, together.",
                    w / 2 - 150,
                    h - 76,
                    300,
                    30,
                    16,
                    Muted,
                    true,
                    TextAlignmentOptions.Center
                );
        }
        FitBoard(top, bottom);
    }

    void Pad(float x, float y, PlayerController player, Color color, string label, float size)
    {
        float b = 64,
            gap = 3;
        // Arrow glyphs follow the isometric world directions, matching keyboard mappings.
        Direction(x, y, "↖", Vector2.up);
        Direction(x + b + gap, y, "↗", Vector2.right);
        Direction(x, y + b + gap, "↙", Vector2.left);
        Direction(x + b + gap, y + b + gap, "↘", Vector2.down);
        Text(screen, label, x, y + 137, 131, 24, 11, Muted, true, TextAlignmentOptions.Center);
        void Direction(float dx, float dy, string glyph, Vector2 dir)
        {
            var rect = Box(screen, label + " " + glyph, dx, dy, b, b, color);
            var img = rect.GetComponent<Image>();
            img.sprite = theme.rounded;
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;
            var arrow = Rect("Direction", rect, 10, 10, b - 20, b - 20)
                .gameObject.AddComponent<GardenArrow>();
            arrow.sprite = theme.arrow;
            arrow.color = Ink;
            arrow.raycastTarget = false;
            arrow.direction =
                dir == Vector2.up ? new Vector2(-1, 1)
                : dir == Vector2.right ? new Vector2(1, 1)
                : dir == Vector2.left ? new Vector2(-1, -1)
                : new Vector2(1, -1);
            var touch = rect.gameObject.AddComponent<GardenTouchButton>();
            touch.player = player;
            touch.direction = dir;
        }
    }

    void FitBoard(float top, float bottom)
    {
        if (boardCamera == null || !hasBoard)
            return;
        float totalH = Screen.height,
            totalW = Screen.width;
        var safe = Screen.safeArea;
        float y = safe.y + (bottom / h) * safe.height;
        float ph = safe.height * (1 - (top + bottom) / h);
        boardCamera.rect = new Rect(safe.x / totalW, y / totalH, safe.width / totalW, ph / totalH);
        float aspect = safe.width / Mathf.Max(1, ph);
        boardCamera.orthographicSize = Mathf.Max(
            (boardBounds.size.y + 1.7f) * .5f,
            (boardBounds.size.x + 1.5f) * .5f / aspect
        );
        boardCamera.transform.position = new Vector3(
            boardBounds.center.x,
            boardBounds.center.y + .12f,
            -10
        );
        boardCamera.clearFlags = CameraClearFlags.SolidColor;
        boardCamera.backgroundColor = Hex("F9E9ED");
        // Preserve the original living sky and fit it to the actual board viewport.
        var sky = boardCamera.transform.Find("SkyBackground");
        if (
            sky != null
            && sky.TryGetComponent<SpriteRenderer>(out var skyRenderer)
            && skyRenderer.sprite != null
        )
        {
            sky.gameObject.SetActive(true);
            Vector2 native = skyRenderer.sprite.bounds.size;
            float cover =
                Mathf.Max(
                    2 * boardCamera.orthographicSize * aspect / native.x,
                    2 * boardCamera.orthographicSize / native.y
                ) * 1.03f;
            sky.localScale = new Vector3(cover, cover, 1);
        }
        var clouds = boardCamera.transform.Find("Clouds");
        if (clouds != null && clouds.TryGetComponent<CloudDrift>(out var drift))
        {
            clouds.gameObject.SetActive(true);
            clouds.localPosition = new Vector3(0, 0, 9.5f);
            drift.FitView(
                2 * boardCamera.orthographicSize * aspect,
                2 * boardCamera.orthographicSize
            );
        }
    }

    void OpenOverlay(string kind)
    {
        if (retrying)
            return;
        if (overlay == null)
            modalReturnScale = Time.timeScale;
        overlay = kind;
        if (Gameplay)
        {
            Time.timeScale = 0;
            PauseMenu.isGamePaused = true;
        }
        if (modal != null)
        {
            modal.gameObject.SetActive(false);
            Destroy(modal.gameObject);
        }
        BuildOverlay();
    }

    void CloseOverlay()
    {
        if (overlay == "win")
            return;
        overlay = null;
        if (modal != null)
        {
            modal.gameObject.SetActive(false);
            Destroy(modal.gameObject);
            modal = null;
        }
        Time.timeScale = modalReturnScale;
        PauseMenu.isGamePaused = false;
    }

    void BuildOverlay()
    {
        modal = Box(screen, "Modal", 0, 0, w, h, new Color(.1f, .18f, .14f, .55f), false);
        modal.GetComponent<Image>().raycastTarget = true;
        float cw = Mathf.Min(w - 32, overlay == "help" && TouchControls && !Portrait ? 740 : 620),
            ch = Mathf.Min(
                h - 24,
                overlay == "help" ? 650
                    : overlay == "settings" ? 480
                    : 460
            );
        var card = Box(modal, "Card", (w - cw) / 2, (h - ch) / 2, cw, ch, Color.white);
        var panelImage = card.GetComponent<Image>();
        panelImage.sprite = theme.panel;
        panelImage.pixelsPerUnitMultiplier = 8;
        float pad = 28;
        string eyebrow =
            overlay == "exit" ? "UNTIL NEXT TIME"
            : overlay == "pause" ? "TAKE A BREATHER"
            : overlay == "help" ? "A PATH FOR TWO"
            : overlay == "win" ? "A LITTLE CLOSER"
            : "MAKE YOURSELF AT HOME";
        string title =
            overlay == "exit" ? "Bye for now?"
            : overlay == "pause" ? "Taking a tiny break?"
            : overlay == "help" ? "How to Play"
            : overlay == "win" ? (sceneIndex == 9 ? "Together, at last." : "You found each other.")
            : "Settings";

        Text(
            card,
            title,
            pad,
            35,
            cw - 2 * pad,
            66,
            TouchControls ? 28 : 34,
            Ink,
            true,
            TextAlignmentOptions.Center
        );
        if (overlay == "exit")
        {
            Text(
                card,
                "There is always another path to explore.",
                pad,
                135,
                cw - 2 * pad,
                65,
                18,
                Muted
            );
            Button(
                card,
                "Stay a little longer",
                pad,
                235,
                cw - 2 * pad,
                52,
                CloseOverlay,
                Green,
                Paper
            );
            Button(card, "Exit game", pad, 302, cw - 2 * pad, 46, Helper.QuitGame, Sage, Ink);
        }
        else if (overlay == "pause")
        {
            Text(
                card,
                "Your little souls will wait for you.",
                pad,
                130,
                cw - 2 * pad,
                35,
                18,
                Muted
            );
            Button(
                card,
                "Keep going",
                pad,
                190,
                cw - 2 * pad,
                56,
                CloseOverlay,
                Green,
                Paper,
                ButtonIcon.Next
            );
            Button(
                card,
                "Try this level again",
                pad,
                260,
                cw - 2 * pad,
                46,
                () => Navigate(sceneIndex),
                Sage,
                Ink
            );
            Button(
                card,
                "Settings",
                pad,
                320,
                (cw - 2 * pad - 12) / 2,
                44,
                () => OpenOverlay("settings"),
                Paper,
                Ink
            );
            Button(
                card,
                "Level selection",
                cw / 2 + 6,
                320,
                (cw - 2 * pad - 12) / 2,
                44,
                () => Navigate(0, true),
                Paper,
                Ink
            );
            Button(card, "Quit game", pad, 378, cw - 2 * pad, 44, Helper.QuitGame, Rose, Ink);
        }
        else if (overlay == "help")
            BuildHelp(card, cw, ch);
        else if (overlay == "settings")
            BuildSettings(card, cw, ch);
        else
            BuildWin(card, cw, ch);
    }

    void BuildHelp(RectTransform card, float cw, float ch)
    {
        bool wide = ch < 500;
        float artW = wide ? cw * .45f : Mathf.Min(cw - 50, 380);
        float artH = wide ? 230 : (Portrait ? 195 : 220);
        Picture(card, theme.tutorial, wide ? 24 : (cw - artW) / 2, 88, artW, artH);
        float objectiveY = wide ? 320 : 88 + artH + 8;
        Text(
            card,
            TouchControls
                ? "Tap each soul’s arrow pad. Bring both to their matching glowing tiles!"
                : "Bring both little souls to their matching glowing tiles!",
            wide ? 30 : 26,
            objectiveY,
            wide ? artW : cw - 52,
            56,
            17,
            Ink,
            true,
            TextAlignmentOptions.Center
        );
        float groupY = wide ? 140 : objectiveY + 69;
        float groupW = wide ? (cw * .48f - 18) / 2 : (cw - 64) / 2;
        ControlLesson(card, wide ? cw * .51f : 24, groupY, groupW, false);
        ControlLesson(card, wide ? cw * .51f + groupW + 12 : cw / 2 + 8, groupY, groupW, true);
        Button(card, "Let's hop!", cw / 2 - 105, ch - 77, 210, 54, CloseOverlay, Green, Ink);
    }

    void ControlLesson(
        Transform parent,
        float x,
        float y,
        float width,
        bool pink,
        bool playable = false,
        float sizeLimit = 46
    )
    {
        Text(
            parent,
            TouchControls
                ? (pink ? "Pink slime" : "Blue slime")
                : (pink ? "Pink · Arrow keys" : "Blue · W A S D"),
            x,
            y,
            width,
            28,
            15,
            Ink,
            true,
            TextAlignmentOptions.Center
        );
        int columns = TouchControls ? 2 : 3;
        float size = Mathf.Min(sizeLimit, (width - (columns - 1) * 6) / columns);
        float left = x + (width - size * columns - (columns - 1) * 6) / 2;
        Vector2[] directions = { Vector2.up, Vector2.right, Vector2.left, Vector2.down };
        string[] letters = { "W", "D", "A", "S" };
        for (int i = 0; i < 4; i++)
        {
            int col =
                TouchControls ? i % 2
                : i == 0 || i == 3 ? 1
                : i == 1 ? 2
                : 0;
            int row =
                TouchControls ? i / 2
                : i == 0 ? 0
                : 1;
            var cap = Box(
                parent,
                (pink ? "Pink " : "Blue ") + letters[i],
                left + col * (size + 6),
                y + 32 + row * (size + 6),
                size,
                size,
                pink ? Rose : Blue
            );
            if (playable)
            {
                cap.GetComponent<Image>().raycastTarget = true;
                var input = cap.gameObject.AddComponent<GardenTouchButton>();
                input.player = pink ? girl : boy;
                input.direction = directions[i];
            }
            if (TouchControls || pink)
            {
                var arrow = Rect("Arrow", cap, 8, 8, size - 16, size - 16)
                    .gameObject.AddComponent<GardenArrow>();
                arrow.sprite = theme.arrow;
                arrow.color = Ink;
                arrow.raycastTarget = false;
                arrow.direction = TouchControls
                    ? new Vector2(i % 2 == 0 ? -1 : 1, i < 2 ? 1 : -1)
                    : directions[i];
            }
            else
                Text(cap, letters[i], 0, 0, size, size, 21, Ink, true, TextAlignmentOptions.Center);
        }
    }

    void BuildSettings(RectTransform card, float cw, float ch)
    {
        Volume(card, "Music", OptionsMenu.MusicPrefKey, "music", 128, cw);
        Volume(card, "Sound effects", OptionsMenu.SFXPrefKey, "sfx", 211, cw);
        bool reduced = PlayerPrefs.GetInt("GardenReducedMotion", 0) == 1;
        Button(
            card,
            "Menu motion: " + (reduced ? "Reduced" : "On"),
            28,
            294,
            cw - 56,
            42,
            () =>
            {
                PlayerPrefs.SetInt("GardenReducedMotion", reduced ? 0 : 1);
                PlayerPrefs.Save();
                OpenOverlay("settings");
            },
            Sage,
            Ink
        );
        Button(
            card,
            "All set",
            28,
            ch - 65,
            cw - 56,
            44,
            () =>
            {
                PlayerPrefs.Save();
                if (Gameplay)
                    OpenOverlay("pause");
                else
                    CloseOverlay();
            },
            Green,
            Paper,
            ButtonIcon.Next
        );
    }

    void Volume(RectTransform card, string label, string pref, string mixerKey, float y, float cw)
    {
        Text(card, label, 28, y, cw - 56, 25, 18, Ink, true);
        var rt = Rect(label, card, 28, y + 34, cw - 56, 28);
        var slider = rt.gameObject.AddComponent<Slider>();
        var bg = Box(rt, "Track", 0, 9, cw - 56, 8, Line);
        var fillArea = Rect("Fill area", rt, 0, 9, cw - 56, 8);
        var fill = Box(fillArea, "Fill", 0, 0, cw - 56, 8, Green);
        var handleArea = Rect("Handle area", rt, 10, 0, cw - 76, 28);
        var handle = Box(handleArea, "Handle", 0, 0, 26, 26, Green);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.sizeDelta = Vector2.zero;
        fill.anchoredPosition = Vector2.zero;
        handle.anchorMin = new Vector2(0, 0);
        handle.anchorMax = new Vector2(0, 1);
        handle.pivot = new Vector2(.5f, .5f);
        handle.sizeDelta = new Vector2(26, 0);
        handle.anchoredPosition = Vector2.zero;
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.minValue = 0;
        slider.maxValue = 1;
        float db = PlayerPrefs.GetFloat(pref, 0);
        slider.SetValueWithoutNotify(db <= -79 ? 0 : Mathf.Pow(10, db / 20));
        slider.onValueChanged.AddListener(value =>
        {
            float vol = value <= .001f ? -80 : Mathf.Log10(value) * 20;
            PlayerPrefs.SetFloat(pref, vol);
            var audio = AudioManager.instance;
            if (audio != null)
            {
                var mixer = audio.audioMixer;
                if (
                    mixer == null
                    && audio.sounds.Length > 0
                    && audio.sounds[0].audioMixerGroup != null
                )
                    mixer = audio.sounds[0].audioMixerGroup.audioMixer;
                if (mixer != null)
                    mixer.SetFloat(mixerKey, vol);
            }
        });
        // The full row is a generous pointer target, not just the thin track.
        var hit = rt.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
    }

    void BuildWin(RectTransform card, float cw, float ch)
    {
        int earned = game != null && game.isLevelCompleted ? game.earnedStars : 3;
        bool preview = game == null || !game.isLevelCompleted;
        bool[] achievements =
        {
            true,
            preview || game.timeStarEarned,
            preview || game.starCollected,
        };
        string[] labels =
        {
            "Together!",
            achievements[1] ? "Quick hops!" : "Try faster",
            achievements[2] ? "Star found!" : "Find the star",
        };
        float starSize = Mathf.Min(86, (cw - 80) / 3);
        var row = Rect(
            "Celebration stars",
            card,
            cw / 2 - starSize * 1.6f,
            125,
            starSize * 3.2f,
            108
        );
        row.gameObject.SetActive(false);
        for (int i = 0; i < 3; i++)
        {
            float size = starSize * (i == 1 ? 1.16f : 1f);
            var slot = Picture(row, theme.star, i * starSize * 1.1f, i == 1 ? -13 : 0, size, size);
            slot.color = new Color(.65f, .6f, .7f, .45f);
            var gold = Picture(slot.transform, theme.star, 0, 0, size, size);
            var flash = Picture(slot.transform, theme.star, 0, 0, size * 1.2f, size * 1.2f);
            flash.color = new Color(1, 1, .9f, 0);
            foreach (var item in new[] { gold, flash })
            {
                item.rectTransform.anchorMin = item.rectTransform.anchorMax = new Vector2(.5f, .5f);
                item.rectTransform.pivot = new Vector2(.5f, .5f);
                item.rectTransform.anchoredPosition = Vector2.zero;
            }
            gold.gameObject.SetActive(achievements[i]);
            Text(
                card,
                labels[i],
                cw / 2 - starSize * 1.6f + i * starSize * 1.1f - 5,
                221,
                starSize + 10,
                24,
                12,
                achievements[i] ? Ink : Muted,
                true,
                TextAlignmentOptions.Center
            );
        }
        var reveal = row.gameObject.AddComponent<WinStars>();
        reveal.earned = earned;
        reveal.earnedSlots = achievements;
        reveal.startDelay = .1f;
        reveal.stagger = .18f;
        row.gameObject.SetActive(true);
        Text(
            card,
            game != null && game.starCollected
                ? "A home shared. A star discovered."
                : "A home shared is a journey worth taking.",
            26,
            255,
            cw - 52,
            36,
            14,
            Muted,
            false,
            TextAlignmentOptions.Center
        );
        Button(
            card,
            sceneIndex == 9 ? "See the ending" : "On to the next place",
            26,
            ch - 155,
            cw - 52,
            52,
            () => Navigate(sceneIndex + 1),
            Green,
            Paper,
            ButtonIcon.Next
        );
        Button(
            card,
            "Play again",
            26,
            ch - 87,
            (cw - 64) / 2,
            44,
            () => Navigate(sceneIndex),
            Sage,
            Ink
        );
        Button(
            card,
            "The garden path",
            cw / 2 + 6,
            ch - 87,
            (cw - 64) / 2,
            44,
            () => Navigate(0, true),
            Sage,
            Ink
        );
    }

    static bool journeyOnLoad;

    IEnumerator NavigateRoutine(int index, bool journey)
    {
        navigating = true;
        PlayerPrefs.Save();
        Time.timeScale = 1;
        PauseMenu.isGamePaused = false;
        var curtain = Box(
            root,
            "Transition",
            0,
            0,
            root.rect.width,
            root.rect.height,
            Paper,
            false
        );
        curtain.GetComponent<Image>().raycastTarget = true;
        var group = curtain.gameObject.AddComponent<CanvasGroup>();
        float elapsed = 0;
        float duration = retrying ? .45f : .2f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = elapsed / duration;
            yield return null;
        }
        journeyOnLoad = journey;
        SceneManager.LoadScene(index);
    }

    public void BeginRetry()
    {
        retrying = true;
        var blocker = Box(
            root,
            "Retry input shield",
            0,
            0,
            root.rect.width,
            root.rect.height,
            Color.clear,
            false
        );
        blocker.GetComponent<Image>().raycastTarget = true;
        blocker.anchorMin = Vector2.zero;
        blocker.anchorMax = Vector2.one;
        blocker.sizeDelta = Vector2.zero;
        blocker.anchoredPosition = Vector2.zero;
        BuildRetryNotice();
    }

    void BuildRetryNotice()
    {
        // Use the reserved HUD band, so the message never covers the falling slime or pads.
        float headerHeight = TouchControls ? 60 : 85;
        float noticeHeight = TouchControls ? 54 : 68;
        float noticeWidth = Mathf.Min(w - 24, 370);
        float x = (w - noticeWidth) / 2,
            y = (headerHeight - noticeHeight) / 2;
        Box(screen, "Retry header", 0, 0, w, headerHeight, Paper, false);
        var notice = Rect("Retry bubble", screen, x, y, noticeWidth, noticeHeight);
        notice.pivot = new Vector2(.5f, .5f);
        notice.anchoredPosition += new Vector2(noticeWidth / 2, -noticeHeight / 2);
        Box(
            notice,
            "Soft shadow",
            0,
            3,
            noticeWidth,
            noticeHeight,
            new Color(.6f, .35f, .5f, .15f)
        );
        Box(notice, "Pink rim", 0, 0, noticeWidth, noticeHeight, Rose);
        Box(notice, "Cream fill", 2, 2, noticeWidth - 4, noticeHeight - 4, Paper);
        Picture(notice, theme.retryLogo, 10, (noticeHeight - 44) / 2, 44, 44);
        Text(
            notice,
            "Oops!",
            60,
            TouchControls ? 5 : 8,
            noticeWidth - 76,
            25,
            TouchControls ? 19 : 21,
            Ink,
            true
        );
        Text(
            notice,
            "Let's try that hop again.",
            60,
            TouchControls ? 29 : 36,
            noticeWidth - 76,
            23,
            TouchControls ? 13 : 15,
            Muted
        );
        if (PlayerPrefs.GetInt("GardenReducedMotion", 0) == 0)
            StartCoroutine(RevealRetryNotice(notice));
    }

    IEnumerator RevealRetryNotice(RectTransform notice)
    {
        var group = notice.gameObject.AddComponent<CanvasGroup>();
        float t = 0;
        while (t < .18f && notice != null)
        {
            t += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(t / .18f);
            group.alpha = progress;
            notice.localScale = Vector3.one * Mathf.Lerp(.94f, 1, 1 - Mathf.Pow(1 - progress, 3));
            yield return null;
        }
        if (notice != null)
        {
            group.alpha = 1;
            notice.localScale = Vector3.one;
        }
    }

    public void Navigate(int index, bool journey = false)
    {
        if (!navigating)
            StartCoroutine(NavigateRoutine(index, journey));
    }

    int Unlocked()
    {
        var data = SaveSystem.LoadData();
        return Mathf.Clamp(data == null ? 1 : data.level, 1, 10);
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && Gameplay && overlay == null && !completed)
            OpenOverlay("pause");
    }

    void OnApplicationFocus(bool focused)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GardenReviewCapture.Running)
            return;
#endif
        if (!focused && Gameplay && overlay == null && !completed && screen != null)
            OpenOverlay("pause");
    }

    static Color Hex(string code)
    {
        ColorUtility.TryParseHtmlString("#" + code, out var color);
        return color;
    }

    static RectTransform Rect(string name, Transform parent, float x, float y, float rw, float rh)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(rw, rh);
        return rt;
    }

    RectTransform Box(
        Transform parent,
        string name,
        float x,
        float y,
        float rw,
        float rh,
        Color color,
        bool round = true
    )
    {
        var rt = Rect(name, parent, x, y, rw, rh);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (round)
        {
            img.sprite = theme.rounded;
            img.type = Image.Type.Sliced;
        }
        return rt;
    }

    Image Picture(Transform parent, Sprite sprite, float x, float y, float rw, float rh)
    {
        var rt = Rect(sprite != null ? sprite.name : "Art", parent, x, y, rw, rh);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    TMP_Text Text(
        Transform parent,
        string value,
        float x,
        float y,
        float rw,
        float rh,
        float size,
        Color color,
        bool bold = false,
        TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft
    )
    {
        var rt = Rect(value.Split('\n')[0], parent, x, y, rw, rh);
        var txt = rt.gameObject.AddComponent<TextMeshProUGUI>();
        txt.font = bold ? theme.heading : theme.body;
        txt.fontSharedMaterial = bold ? theme.headingMaterial : theme.bodyMaterial;
        txt.text = value;
        txt.fontSize = size;
        txt.color = color;
        txt.alignment = alignment;
        txt.enableWordWrapping = true;
        txt.raycastTarget = false;
        txt.margin = Vector4.zero;
        return txt;
    }

    enum ButtonIcon
    {
        None,
        Back,
        Next,
        Help,
        Pause,
    }

    Button Button(
        Transform parent,
        string label,
        float x,
        float y,
        float rw,
        float rh,
        Action action,
        Color bg,
        Color fg,
        ButtonIcon symbol = ButtonIcon.None
    )
    {
        var rt = Box(parent, label, x, y, rw, rh, bg);
        var img = rt.GetComponent<Image>();
        img.sprite = theme.button;
        img.pixelsPerUnitMultiplier = theme.button.rect.height / rh;
        img.color = bg;
        img.raycastTarget = true;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var colors = b.colors;
        colors.highlightedColor = new Color(1f, .95f, .98f);
        colors.pressedColor = new Color(.92f, .82f, .9f);
        colors.disabledColor = new Color(1, 1, 1, .65f);
        b.colors = colors;
        bool backArrow = symbol == ButtonIcon.Back;
        bool forwardArrow = symbol == ButtonIcon.Next;
        string caption = symbol == ButtonIcon.Help || symbol == ButtonIcon.Pause ? "" : label;
        var captionText = Text(
            rt,
            caption,
            10,
            0,
            rw - 20,
            rh,
            TouchControls ? 15 : 17,
            fg,
            true,
            TextAlignmentOptions.Center
        );
        if (backArrow || forwardArrow)
        {
            float iconSize = 18;
            float textWidth = Mathf.Min(captionText.preferredWidth, rw - 60);
            float gap = 12;
            float groupX = (rw - textWidth - gap - iconSize) / 2;
            captionText.rectTransform.anchoredPosition = new Vector2(
                groupX + (backArrow ? iconSize + gap : 0),
                0
            );
            captionText.rectTransform.sizeDelta = new Vector2(textWidth + .5f, rh);
            var icon = Picture(
                rt,
                theme.arrow,
                backArrow ? groupX : groupX + textWidth + gap,
                (rh - iconSize) / 2,
                iconSize,
                iconSize
            );
            icon.color = fg;
            icon.rectTransform.pivot = new Vector2(.5f, .5f);
            icon.rectTransform.anchoredPosition += new Vector2(iconSize / 2, -iconSize / 2);
            icon.rectTransform.localRotation = Quaternion.Euler(0, 0, backArrow ? 90 : -90);
        }
        if (symbol == ButtonIcon.Help || symbol == ButtonIcon.Pause)
        {
            var glyph = Picture(
                rt,
                symbol == ButtonIcon.Help ? theme.helpIcon : theme.pauseIcon,
                (rw - 24) / 2,
                (rh - 24) / 2,
                24,
                24
            );
            glyph.color = fg;
        }
        b.onClick.AddListener(() =>
        {
            if (navigating)
                return;
            AudioManager.instance?.Play("Click");
            action();
        });
        return b;
    }
}
