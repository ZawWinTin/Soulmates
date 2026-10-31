#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Opt-in local visual QA, enabled only by --garden-review in development builds.</summary>
public sealed class GardenReviewCapture : MonoBehaviour
{
    public static bool Running { get; private set; }
    string output;
    bool retryOnly;
    bool braidOnly;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--garden-review");
        if (index < 0 || index + 1 >= args.Length)
            return;
        Running = true;
        Application.runInBackground = true;
        var capture = new GameObject("Garden review capture").AddComponent<GardenReviewCapture>();
        capture.output = args[index + 1];
        capture.retryOnly = Array.IndexOf(args, "--retry-review") >= 0;
        capture.braidOnly = Array.IndexOf(args, "--braid-review") >= 0;
        DontDestroyOnLoad(capture.gameObject);
    }

    IEnumerator Start()
    {
        Directory.CreateDirectory(output);
        if (braidOnly)
        {
            yield return BraidFrames();
            Application.Quit();
            yield break;
        }
        if (retryOnly)
        {
            foreach (
                var size in new[]
                {
                    new Vector2Int(1200, 800),
                    new Vector2Int(390, 844),
                    new Vector2Int(844, 390),
                }
            )
            {
                yield return SceneManager.LoadSceneAsync(1);
                Screen.SetResolution(size.x, size.y, false);
                yield return new WaitForSecondsRealtime(.5f);
                GardenInterface.Active.SetTouchPreview(size.x != 1200);
                var previous = FindObjectOfType<GameController>();
                previous.GameOver();
                yield return Shot("retry-" + size.x + "x" + size.y);
                yield return new WaitForSecondsRealtime(.6f);
                if (previous != null || SceneManager.GetActiveScene().buildIndex != 1)
                    Debug.LogError(
                        "[GardenReviewCapture.Start] Retry review: same-level restart failed"
                    );
            }
            File.WriteAllText(
                Path.Combine(output, "retry-complete.txt"),
                "Retry bubble captured at desktop, portrait and landscape sizes; same-level restart checked."
            );
            Application.Quit();
            yield break;
        }
        Screen.SetResolution(1200, 800, false);
        yield return new WaitForSecondsRealtime(2);
        yield return Shot("01-desktop-home");
        GardenInterface.Active.ShowPage("credits");
        yield return Shot("desktop-credits");
        GardenInterface.Active.ShowPage("home");
        GardenInterface.Active.ShowPage("journey");
        yield return Shot("02-desktop-journey");
        GardenInterface.Active.ShowPage("home");
        GardenInterface.Active.PreviewOverlay("settings");
        yield return Shot("03-desktop-settings");
        GardenInterface.Active.PreviewOverlay("help");
        yield return Shot("desktop-help");
        yield return SceneManager.LoadSceneAsync(0);
        Screen.SetResolution(390, 844, false);
        yield return new WaitForSecondsRealtime(1);
        GardenInterface.Active.SetTouchPreview(true);
        yield return Shot("04-phone-home");
        GardenInterface.Active.ShowPage("credits");
        yield return Shot("phone-credits");
        GardenInterface.Active.ShowPage("home");
        GardenInterface.Active.ShowPage("journey");
        yield return Shot("05-phone-journey");
        GardenInterface.Active.PreviewOverlay("help");
        yield return Shot("06-phone-help");
        yield return SceneManager.LoadSceneAsync(1);
        Screen.SetResolution(844, 390, false);
        yield return new WaitForSecondsRealtime(1);
        GardenInterface.Active.SetTouchPreview(true);
        yield return Shot("07-phone-landscape-game");
        // Verify unsupported directions cannot consume a tile or start a hop.
        var pc = GameObject.FindGameObjectWithTag("Player1").GetComponent<PlayerController>();
        var start = pc.transform.position;
        pc.RequestMove(Vector2.one.normalized);
        if (pc.isMoving || pc.transform.position != start)
            Debug.LogError(
                "[GardenReviewCapture.Start] Garden review: diagonal input incorrectly moved player"
            );
        Screen.SetResolution(390, 844, false);
        yield return Shot("08-phone-portrait-game");
        GardenInterface.Active.PreviewOverlay("pause");
        yield return Shot("09-phone-pause");
        yield return SceneManager.LoadSceneAsync(1);
        Screen.SetResolution(1200, 800, false);
        yield return new WaitForSecondsRealtime(1);
        yield return Shot("10-desktop-game");
        GardenInterface.Active.PreviewOverlay("win");
        yield return Shot("11-desktop-result");
        var starReveal = FindObjectOfType<WinStars>();
        starReveal.earnedSlots = new[] { true, false, true };
        starReveal.enabled = false;
        starReveal.enabled = true;
        yield return new WaitForSecondsRealtime(1);
        for (int slot = 0; slot < 3; slot++)
        {
            float revealedScale = starReveal.transform.GetChild(slot).GetChild(0).localScale.x;
            if (slot == 1 ? revealedScale > .01f : revealedScale < .99f)
                Debug.LogError(
                    "[GardenReviewCapture.Start] Garden review: incorrect achievement star slot "
                        + slot
                );
        }
        yield return SceneManager.LoadSceneAsync(10);
        yield return Shot("12-desktop-credits");
        foreach (var size in new[] { new Vector2Int(844, 390), new Vector2Int(390, 844) })
        {
            yield return SceneManager.LoadSceneAsync(0);
            Screen.SetResolution(size.x, size.y, false);
            yield return new WaitForSecondsRealtime(.5f);
            GardenInterface.Active.SetTouchPreview(true);
            string mode = size.x > size.y ? "landscape" : "portrait";
            foreach (string page in new[] { "home", "journey", "credits" })
            {
                GardenInterface.Active.ShowPage(page);
                yield return Shot(mode + "-" + page);
            }
            GardenInterface.Active.ShowPage("home");
            foreach (string panel in new[] { "settings", "help", "exit" })
            {
                GardenInterface.Active.PreviewOverlay(panel);
                yield return Shot(mode + "-" + panel);
            }
        }
        Screen.SetResolution(1200, 800, false);
        yield return new WaitForSecondsRealtime(.5f);
        for (int level = 1; level <= 9; level++)
        {
            yield return SceneManager.LoadSceneAsync(level);
            yield return new WaitForSecondsRealtime(.3f);
            GardenInterface.Active.SetTouchPreview(true);
            yield return Shot("level-" + level.ToString("00"));
            var tiles = GameObject.FindGameObjectWithTag("GroundTileMap").GetComponent<Tilemap>();
            var pads = FindObjectsOfType<GardenTouchButton>();
            var pressed = new System.Collections.Generic.List<GardenTouchButton>();
            foreach (string tag in new[] { "Player1", "Player2" })
            {
                var player = GameObject.FindGameObjectWithTag(tag).GetComponent<PlayerController>();
                foreach (var pad in pads)
                {
                    if (pad.player != player)
                        continue;
                    Vector3 delta =
                        pad.direction == Vector2.up ? new Vector3(-.5f, .25f)
                        : pad.direction == Vector2.right ? new Vector3(.5f, .25f)
                        : pad.direction == Vector2.left ? new Vector3(-.5f, -.25f)
                        : new Vector3(.5f, -.25f);
                    if (!tiles.HasTile(tiles.WorldToCell(player.transform.position + delta)))
                        continue;
                    pad.OnPointerDown(
                        new PointerEventData(EventSystem.current)
                        {
                            pointerId = tag == "Player1" ? 1 : 2,
                        }
                    );
                    if (!player.isMoving)
                        Debug.LogError(
                            "[GardenReviewCapture.Start] Garden review: touch did not move "
                                + tag
                                + " level "
                                + level
                        );
                    pressed.Add(pad);
                    break;
                }
            }
            foreach (var pad in pressed)
                pad.OnPointerUp(
                    new PointerEventData(EventSystem.current)
                    {
                        pointerId = pad.player.CompareTag("Player1") ? 1 : 2,
                    }
                );
            yield return new WaitForSecondsRealtime(.36f);
            GardenInterface.Active.PreviewOverlay("pause");
            foreach (var pad in pads)
            {
                if (pad == null)
                    continue;
                var before = pad.player.transform.position;
                pad.OnPointerDown(new PointerEventData(EventSystem.current) { pointerId = 5 });
                if (pad.player.transform.position != before)
                    Debug.LogError("[GardenReviewCapture.Start] Garden review: moved while paused");
            }
        }
        yield return SceneManager.LoadSceneAsync(1);
        yield return new WaitForSecondsRealtime(.5f);
        // Exercise the real Input System bindings, not just the pointer handler.
        // Use an isolated virtual device: native keyboards are disabled by Unity when
        // the automated player is behind the editor. This setting is review-only.
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.EnableDevice(keyboard);
        foreach (bool pink in new[] { false, true })
        {
            yield return SceneManager.LoadSceneAsync(1);
            yield return new WaitForSecondsRealtime(.3f);
            var controlled = GameObject
                .FindGameObjectWithTag(pink ? "Player2" : "Player1")
                .GetComponent<PlayerController>();
            var ground = GameObject.FindGameObjectWithTag("GroundTileMap").GetComponent<Tilemap>();
            Key[] keys = pink
                ? new[] { Key.UpArrow, Key.RightArrow, Key.LeftArrow, Key.DownArrow }
                : new[] { Key.W, Key.D, Key.A, Key.S };
            Vector3[] moves =
            {
                new Vector3(-.5f, .25f),
                new Vector3(.5f, .25f),
                new Vector3(-.5f, -.25f),
                new Vector3(.5f, -.25f),
            };
            bool exercised = false;
            for (int i = 0; i < keys.Length; i++)
            {
                if (!ground.HasTile(ground.WorldToCell(controlled.transform.position + moves[i])))
                    continue;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[i]));
                yield return null;
                yield return null;
                if (!controlled.isMoving)
                    Debug.LogError(
                        "[GardenReviewCapture.Start] Garden review: keyboard binding failed: "
                            + keys[i]
                    );
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                exercised = true;
                break;
            }
            if (!exercised)
                Debug.LogError(
                    "[GardenReviewCapture.Start] Garden review: no keyboard movement was exercised"
                );
        }
        yield return SceneManager.LoadSceneAsync(1);
        yield return new WaitForSecondsRealtime(.3f);
        var falling = GameObject.FindGameObjectWithTag("Player1").GetComponent<PlayerController>();
        var partner = GameObject.FindGameObjectWithTag("Player2").GetComponent<PlayerController>();
        falling.transform.position += Vector3.left * 20;
        yield return new WaitForSecondsRealtime(.25f);
        if (falling == null || !partner.InputLocked)
            Debug.LogError(
                "[GardenReviewCapture.Start] Garden review: fall reloaded instantly or failed to lock input"
            );
        yield return Shot("fall-before-retry");
        yield return new WaitForSecondsRealtime(1.5f);
        if (falling != null || SceneManager.GetActiveScene().buildIndex != 1)
            Debug.LogError(
                "[GardenReviewCapture.Start] Garden review: delayed retry did not reload the same level"
            );
        // Interrupt an actual idle flip and ensure the next hop starts from its grounded cell.
        yield return SceneManager.LoadSceneAsync(1);
        yield return new WaitForSecondsRealtime(.3f);
        int motionPreference = PlayerPrefs.GetInt("GardenReducedMotion", 0);
        PlayerPrefs.SetInt("GardenReducedMotion", 0);
        var actor = GameObject.FindGameObjectWithTag("Player1").GetComponent<PlayerController>();
        var home = actor.transform.position;
        typeof(PlayerController)
            .GetField(
                "nextFun",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )
            .SetValue(actor, 0f);
        yield return new WaitForSecondsRealtime(.15f);
        if (!actor.IsIdleFlipping)
            Debug.LogError("[GardenReviewCapture.Start] Animation review: idle flip did not begin");
        actor.RequestMove(Vector2.up);
        if (
            !actor.isMoving
            || actor.IsIdleFlipping
            || Vector3.Distance(actor.transform.position, home) > .02f
        )
            Debug.LogError(
                "[GardenReviewCapture.Start] Animation review: input failed to interrupt and ground idle flip"
            );
        yield return new WaitForSecondsRealtime(.4f);
        if (Vector3.Distance(actor.transform.position, home + new Vector3(-.5f, .25f, 0)) > .02f)
            Debug.LogError(
                "[GardenReviewCapture.Start] Animation review: interrupted flip changed hop destination"
            );

        foreach (bool reduced in new[] { false, true })
        {
            yield return SceneManager.LoadSceneAsync(1);
            Screen.SetResolution(1200, 800, false);
            yield return new WaitForSecondsRealtime(.3f);
            PlayerPrefs.SetInt("GardenReducedMotion", reduced ? 1 : 0);
            var blue = GameObject.FindGameObjectWithTag("Player1").GetComponent<PlayerController>();
            var pink = GameObject.FindGameObjectWithTag("Player2").GetComponent<PlayerController>();
            var game = FindObjectOfType<GameController>();
            blue.transform.position = GameObject
                .FindGameObjectWithTag("WinTile1")
                .transform.position;
            blue.SendMessage("CheckWinning");
            yield return Shot(reduced ? "rune-reduced" : "rune-arrival");
            if (!blue.isPlayerWinning || blue.currentLift != 0 || game.isLevelCompleted)
                Debug.LogError(
                    "[GardenReviewCapture.Start] Animation review: first arrival failed to settle and wait"
                );
            pink.transform.position = GameObject
                .FindGameObjectWithTag("WinTile2")
                .transform.position;
            pink.SendMessage("CheckWinning");
            yield return new WaitForSecondsRealtime(.2f);
            if (!game.isLevelCompleted || game.ResultsReady)
                Debug.LogError(
                    "[GardenReviewCapture.Start] Animation review: shared celebration did not precede results"
                );
            ScreenCapture.CaptureScreenshot(
                Path.Combine(output, reduced ? "together-reduced.png" : "together-celebration.png")
            );
            yield return new WaitForSecondsRealtime(1.2f);
            if (!game.ResultsReady)
                Debug.LogError(
                    "[GardenReviewCapture.Start] Animation review: results were never revealed"
                );
            yield return Shot(reduced ? "result-reduced" : "result-celebrated");
        }
        PlayerPrefs.SetInt("GardenReducedMotion", motionPreference);
        File.WriteAllText(
            Path.Combine(output, "complete.txt"),
            "Captured desktop and phone layouts and all nine levels; diagonal guard, dual touch input, paused input, keyboard bindings, achievement star slots, delayed fall retry, idle-flip interruption, arrival settling, shared celebration, and reduced motion checked."
        );
        Application.Quit();
    }

    // Frame-by-frame capture of the soul braid: put Player2 on Player1's tile so they stack, then
    // save every frame at a fixed 30 fps game clock. braid-focus.txt has the couple's pixel position.
    IEnumerator BraidFrames()
    {
        yield return SceneManager.LoadSceneAsync(1);
        Screen.SetResolution(1200, 800, false);
        yield return new WaitForSecondsRealtime(1.5f);
        var p1 = GameObject.FindGameObjectWithTag("Player1").transform;
        var p2 = GameObject.FindGameObjectWithTag("Player2").transform;
        Time.captureFramerate = 30;
        p2.position = p1.position;
        File.WriteAllText(
            Path.Combine(output, "braid-focus.txt"),
            Camera.main.WorldToScreenPoint(p1.position) + " " + Screen.width + "x" + Screen.height
        );
        for (int frame = 0; frame < 84; frame++)
        {
            ScreenCapture.CaptureScreenshot(
                Path.Combine(output, "braid-" + frame.ToString("000") + ".png")
            );
            yield return null;
        }
        Time.captureFramerate = 0;
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForSecondsRealtime(.7f);
        Canvas.ForceUpdateCanvases();
        foreach (var arrow in FindObjectsOfType<GardenArrow>())
        {
            var parent = (RectTransform)arrow.transform.parent;
            var center = (Vector2)
                parent.InverseTransformPoint(
                    arrow.rectTransform.TransformPoint(arrow.rectTransform.rect.center)
                );
            if (Vector2.Distance(center, parent.rect.center) > .1f)
                Debug.LogError(
                    "[GardenReviewCapture.Shot] Garden review: arrow not centered in key in " + name
                );
            var renderer = arrow.GetComponent<CanvasRenderer>();
            var mesh = renderer != null ? renderer.GetMesh() : null;
            if (mesh == null || mesh.vertexCount == 0)
                Debug.LogError(
                    "[GardenReviewCapture.Shot] Garden review: missing arrow geometry in " + name
                );
        }
        foreach (var text in FindObjectsOfType<TMP_Text>())
            if (text.isTextOverflowing)
                Debug.LogWarning(
                    "[GardenReviewCapture.Shot] Garden layout overflow: " + name + " / " + text.text
                );
        ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
        yield return new WaitForSecondsRealtime(.5f);
    }
}
#endif
