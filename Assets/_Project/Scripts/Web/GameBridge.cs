using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// ─────────────────────────────────────────────────────────────────────────────
// GameBridge — the Unity side of the z-games web bridge.
//
// The z-games site (react-unity-webgl) talks to this object by name:
//     sendMessage("GameBridge", "LoadData", json)   // push cloud save into game
//     sendMessage("GameBridge", "SetContext", json) // who is playing (guest?)
//     sendMessage("GameBridge", "SetDevice", json)  // what they're playing ON
// and Unity talks back through Assets/Plugins/WebGL/ZGamesBridge.jslib:
//     "Ready"    — emitted once on boot (site then sends SetContext + LoadData)
//     "SaveData" — emitted with the progress blob whenever local progress saves
//     "exit"     — emitted by Helper.QuitGame() (already existed)
//
// Progress blob format (must round-trip through LoadData below):
//     {"level":4,"stars":[0,3,2,1,0,0,0,0,0,0, ...32 entries]}
//   level         highest UNLOCKED level (SavedData.level; new player = 1)
//   stars[i]      best star rating 0..3 for scene build index i
//                 (Level01 = build index 1 … Level09 = 9; index 0 is Menu, unused)
//
// Laravel analogy: this class is a tiny controller + event dispatcher. The site
// is the "client" calling routes (LoadData/SetContext); NotifyProgressSaved is
// like firing a queued event the site listens to and persists.
//
// The object creates ITSELF at startup (Bootstrap below), so no scene or prefab
// needs editing — it exists in every scene, in Editor play mode too (where the
// jslib calls are replaced by Debug.Log so desktop testing keeps working).
// ─────────────────────────────────────────────────────────────────────────────
public class GameBridge : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    // Implemented in Assets/Plugins/WebGL/ZGamesBridge.jslib.
    [DllImport("__Internal")]
    private static extern void ZGamesNotifyReady();

    [DllImport("__Internal")]
    private static extern void ZGamesNotifySave(string json);
#endif

    // ── Context pushed by the site via SetContext (future use, no behavior yet) ──
    // Defaults to guest until the site tells us otherwise (conservative).
    public static bool IsGuest { get; private set; } = true;
    public static string UserName { get; private set; } = null;

    // ── Device pushed by the site via SetDevice ──────────────────────────────────
    // We CANNOT work this out from inside the build: in WebGL,
    // Application.isMobilePlatform is false and SystemInfo.deviceType returns
    // Desktop even on a phone, and Touchscreen.current is non-null on any
    // touchscreen laptop. Only the browser knows, so it tells us — on boot and
    // again whenever it changes (rotation, a keyboard being paired).
    //
    // Read IsTouch to decide whether to show on-screen controls, and subscribe
    // to DeviceChanged to react while running:
    //     void OnEnable()  { GameBridge.DeviceChanged += Apply; Apply(); }
    //     void OnDisable() { GameBridge.DeviceChanged -= Apply; }
    //     void Apply()     { pad.SetActive(GameBridge.IsTouch); }
    //
    // Keyboard input must keep working when IsTouch is true — hybrid devices
    // are real, and "does a physical keyboard exist" is not detectable.
    public static bool IsTouch { get; private set; } = false;
    public static bool IsPortrait { get; private set; } = false;

    /// Raised after SetDevice changes IsTouch or IsPortrait. Static, because the
    /// bridge outlives every scene — unsubscribe in OnDisable.
    ///
    /// Fully qualified, like System.Exception below: a bare `using System;`
    /// makes `Object` ambiguous with UnityEngine.Object (CS0104) further down.
    public static event System.Action DeviceChanged;

    static GameBridge instance;

    // True while LoadData is merging a cloud save into local storage. The merge
    // writes through SaveSystem, and SaveSystem notifies us on every write — this
    // flag stops those writes from echoing dozens of SaveData events back to the
    // site mid-merge (one combined event is sent at the end instead).
    static bool suppressNotify = false;

    // False until the site has sent its first LoadData. Local writes before that
    // (e.g. PlayOptions saving the default "level 1" for a new player at menu
    // Awake) must NOT be pushed to the cloud yet — doing so would overwrite the
    // real cloud save before we've had a chance to load it. Once the cloud copy
    // has been merged in, saves flow to the site normally.
    static bool cloudLoaded = false;

    // Runs automatically after the first scene loads — no scene/prefab wiring
    // needed. Creates the "GameBridge" GameObject the site sends messages to.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        // The name MUST be exactly "GameBridge" — the site targets it by name.
        GameObject go = new GameObject("GameBridge");
        instance = go.AddComponent<GameBridge>();
        DontDestroyOnLoad(go); // survive scene changes so SendMessage always lands
    }

    void Start()
    {
        // Tell the site we booted. It answers with SetContext, then LoadData
        // (the cloud save blob, or "" if there is none / player is a guest).
#if UNITY_WEBGL && !UNITY_EDITOR
        ZGamesNotifyReady();
#else
        Debug.Log("GameBridge: Ready (editor/desktop — event not sent)");
#endif
    }

    // ── DTOs for JsonUtility (Unity's built-in JSON — no external libs) ──────────

    [System.Serializable]
    public class ProgressBlob
    {
        public int level; // highest unlocked level
        public int[] stars; // best stars per scene build index
    }

    [System.Serializable]
    public class ContextBlob
    {
        public bool isGuest = true;
        public string userName; // may be absent in the JSON → stays null
    }

    [System.Serializable]
    public class DeviceBlob
    {
        public bool isTouch = false;
        public bool isPortrait = false;
    }

    // ── JS → Unity (called by the site via SendMessage — names are the contract) ─

    // Cloud save arriving from the site. Empty string means "no cloud save".
    // Merge is CONSERVATIVE — we only ever raise progress, never lower it:
    //   • level: applied only if the cloud value is higher than local
    //   • stars: per level, the higher of cloud vs local wins
    // so playing on two devices can't wipe progress in either direction.
    public void LoadData(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.Log("GameBridge: no cloud save (fresh game or guest)");
            cloudLoaded = true; // nothing to merge, but local saves may now sync
            return;
        }

        ProgressBlob cloud = null;
        try
        {
            cloud = JsonUtility.FromJson<ProgressBlob>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("GameBridge: could not parse cloud save, ignoring. " + e.Message);
            return;
        }
        if (cloud == null)
            return;

        bool changed = false;
        suppressNotify = true; // batch: one SaveData event after the merge, not one per write
        try
        {
            // Unlocked level: take the max of cloud vs local.
            SavedData local = SaveSystem.LoadData();
            int localLevel = local != null ? local.level : 0;
            if (cloud.level > localLevel)
            {
                SaveSystem.SaveData(cloud.level);
                changed = true;
            }

            // Stars: per build index, keep the best rating. SaveSystem.SaveStars
            // already only writes when the new count is HIGHER, so it is the max.
            if (cloud.stars != null)
            {
                for (int i = 0; i < cloud.stars.Length; i++)
                {
                    if (cloud.stars[i] > SaveSystem.GetStars(i))
                    {
                        SaveSystem.SaveStars(i, cloud.stars[i]);
                        changed = true;
                    }
                }
            }
        }
        finally
        {
            suppressNotify = false;
        }

        // From here on, local saves are allowed to sync to the cloud — the cloud
        // copy has been loaded, so we can no longer clobber it.
        cloudLoaded = true;

        // If the merge improved local state, echo the combined result back so the
        // cloud copy is updated to the merged progress too, and refresh the level
        // menu — it was built at Awake, before this cloud save arrived.
        if (changed)
        {
            NotifyProgressSaved();
            RefreshMenu();
        }
    }

    // Re-evaluate the level-select menu after a cloud merge so restored progress
    // (unlocked levels / stars) shows immediately instead of only after a scene
    // reload. No-op when the menu isn't the active scene.
    static void RefreshMenu()
    {
        PlayOptions menu = Object.FindObjectOfType<PlayOptions>();
        if (menu != null)
            menu.RefreshLevels();
    }

    // Who is playing. Payload: { "isGuest": bool, "userName": string? }.
    // Stored for future use (e.g. hiding "progress is saved online" hints for
    // guests) — deliberately no gameplay behavior change yet.
    public void SetContext(string json)
    {
        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            ContextBlob ctx = JsonUtility.FromJson<ContextBlob>(json);
            if (ctx != null)
            {
                IsGuest = ctx.isGuest;
                UserName = ctx.userName;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("GameBridge: could not parse context, ignoring. " + e.Message);
        }
    }

    // What they're playing on. Payload: { "isTouch": bool, "isPortrait": bool }.
    // Sent on boot AND on every change, so this must stay idempotent — it fires
    // DeviceChanged only when a value actually moved.
    public void SetDevice(string json)
    {
        if (string.IsNullOrEmpty(json))
            return;

        DeviceBlob device;
        try
        {
            device = JsonUtility.FromJson<DeviceBlob>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("GameBridge: could not parse device, ignoring. " + e.Message);
            return;
        }
        if (device == null)
            return;

        bool changed = device.isTouch != IsTouch || device.isPortrait != IsPortrait;
        IsTouch = device.isTouch;
        IsPortrait = device.isPortrait;

        if (!changed)
            return;

        // A listener throwing must not take the bridge down with it — the site
        // gets no feedback from SendMessage, so a swallowed exception here would
        // look like the message never arrived.
        try
        {
            DeviceChanged?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("GameBridge: a DeviceChanged listener threw. " + e.Message);
        }
    }

    // ── Unity → JS ───────────────────────────────────────────────────────────────

    // Called by SaveSystem after every successful local write. Serializes the
    // CURRENT local progress and hands it to the site as the "SaveData" event
    // (the site persists it for signed-in users; ignores it for guests).
    // Safe to call anywhere: outside WebGL it just logs.
    public static void NotifyProgressSaved()
    {
        if (suppressNotify)
            return;

        // Don't push local writes to the cloud until the cloud save has been
        // loaded — otherwise a new player's default "level 1" (written at menu
        // Awake) could overwrite real cloud progress before it's fetched.
        if (!cloudLoaded)
            return;

        SavedData local = SaveSystem.LoadData();
        if (local == null)
        {
            // Local is missing or unreadable (corrupt file). Pushing a zeroed blob
            // now would, under the cloud's last-write-wins, clobber good progress
            // on z-core. Skip — a real save syncs once local is readable again.
            Debug.LogWarning("GameBridge: skipping cloud sync — no readable local save.");
            return;
        }

        ProgressBlob blob = new ProgressBlob();
        blob.level = local.level;
        blob.stars = local.stars != null ? local.stars : new int[32];

        string json = JsonUtility.ToJson(blob);

#if UNITY_WEBGL && !UNITY_EDITOR
        ZGamesNotifySave(json);
#else
        Debug.Log("GameBridge: SaveData (editor/desktop — event not sent): " + json);
#endif
    }
}
