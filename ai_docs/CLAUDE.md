> **Current design update (September 2026):** `Assets/_Project/Scripts/GardenInterface.cs` owns menu/HUD/modal navigation and creates the UI at runtime from `GardenTheme`. `GardenTouchButton` supports simultaneous touch controls; `GardenArrow` renders font-independent direction icons. `Editor/GardenDesignSetup` installs the design into all eleven scenes through Unity APIs. Legacy canvases are inactive; do not run the old ArtRevampSetup commands over this branch. Read `Design/DESIGN.md` for current details; the older architecture notes below are historical.

# Soulmates — Project Guide

This document is the entry point for any AI assistant (or new contributor) working on **Soulmates**. Read it before making changes; update it when the project's shape changes.

> **Audience note:** the project owner's primary background is PHP/Laravel and they are an amateur with Unity. Explain Unity-specific concepts when relevant; favor explicit, opinionated guidance over open-ended "it depends" answers. Their sister project, **The-Lost-Scrolls**, follows the same conventions.

---

## 1. What this project is

A **two-player cooperative puzzle-platformer** with isometric tile-destruction mechanics. Players control two kawaii slime characters; moving destroys tiles beneath them, stacking on the same tile triggers a heart animation, and each level requires both players to reach their respective goal tiles before falling off the grid.

Visual style: cute pixel-art with kawaii blob sprites and parallax cloud backgrounds.

Genre: cooperative puzzle-platformer (think *Lovers in a Dangerous Spacetime* meets *Crossy Road*).

---

## 2. Engine & dependency stack

| Thing | Version | Notes |
|---|---|---|
| Unity Editor | **2022.3.62f3 LTS** | Recently upgraded from 2021.3.37f1. Project also kept on this version to match its sister project The-Lost-Scrolls. |
| Render pipeline | **URP 14.0.12** (2D Renderer) | HDRP package is also installed but unused — slated for cleanup. |
| Input | **Unity Input System 1.14.0** | New input system, not legacy `Input.GetKey`. Action map is `Assets/Scripts/PlayersMovement.inputactions`. |
| 2D toolchain | **2D Animation 9.2.0**, **PSD Importer 8.1.0**, **Pixel Perfect 5.1.0**, **SpriteShape 9.1.0** | All 2022-LTS versions. |
| Recorder | **4.0.3** | For capturing gameplay clips. |
| TMP | **3.0.7** | Bundled fonts under `Assets/TextMesh Pro/`. |
| Tests | None | No EditMode or PlayMode tests exist. |
| Assembly definitions | None | All gameplay code compiles into the default `Assembly-CSharp`. |
| Namespaces | None | All scripts live in the global namespace. |

---

## 3. Folder layout (current, actual)

```
Assets/
├── Scripts/                  # All gameplay C# (22 files, FLAT — no subfolders). See §4.
├── Scenes/                   # 9 levels + Menu/Demo/Credit (12 .unity files total)
├── Prefabs/                  # AudioManager, GameController, LevelLoader, Camera, UI canvases
├── Sprites/                  # Backgrounds, slime spritesheets, UI buttons, clouds
├── Animations/               # Character animation clips (jump/walk states)
├── Audios/                   # SFX (jump, fall) and background music
├── Tiles/                    # Grass tilesheet variants
├── Shaders/                  # Tile Dissolve.shadergraph (custom dissolve effect)
├── Fonts/                    # TextMesh Pro fonts
├── MainMixer.mixer           # Audio mixer (master/SFX/music channels)
├── Renderer/                 # URP renderer asset
├── HDRPDefaultResources/     # ⚠ Unused — URP project, HDRP residue
├── Kawaii Slimes/            # ⚠ Third-party sprite pack (in Assets root, not Resources/)
├── TextMesh Pro/             # ⚠ TMP runtime resources (Unity-bundled)
└── UniversalRenderPipelineGlobalSettings.asset

ai_docs/                      # Human + AI documentation
```

**Laravel analogy:** `Assets/Scripts/` ≈ `app/`, `Prefabs/` ≈ Blade partials (composable game objects), `Scenes/` ≈ route-level views, asset packs in Assets root ≈ poorly-placed `vendor/` (would normally live under `Plugins/` or `ThirdParty/`).

---

## 4. Code architecture

### Core systems

| System | Key files | Notes |
|---|---|---|
| **Game state** | `Scripts/GameController.cs` | Detects when both players land on the same tile (heart animation), tracks win condition (both players on goal tiles), tracks loss (player falls off grid). The combined "stacked" sprite is rendered when both players are on the same tile. |
| **Player movement** | `Scripts/PlayerController.cs` (~312 LOC), `Scripts/PlayersMovement.cs` (auto-generated) | Isometric grid-based movement with 0.35s smooth tile-to-tile interpolation. Uses Unity's New Input System. Triggers tile dissolution shader on departure, applies falling physics + rotation when player drops off the grid, updates sprite sorting order on dropped tiles. |
| **Lighting** | `Scripts/PlayerLightController.cs` | "Breathing light" effect: sine-wave modulation of a `Light2D`'s intensity (0.5–0.8) and outer radius (0.3–0.8). Configurable speed. |
| **Camera** | `Scripts/CameraController.cs` | Follows the midpoint between the two players. |
| **Levels & flow** | `Scripts/LevelLoader.cs` | Scene transitions with fade animation; configurable transition timing. |
| **Persistence** | `Scripts/SaveSystem.cs`, `Scripts/SavedData.cs` | Binary serialization of progress (last completed level) to `Application.persistentDataPath/level.pz`. **Uses `BinaryFormatter` — see §7.** |
| **Audio** | `Scripts/AudioManager.cs`, `Scripts/Sound.cs` | String-keyed sound lookup over a serialized `Sound[]` array (Inspector-wired). Same pattern as The-Lost-Scrolls. |
| **UI / Menus** | `Scripts/MainMenu.cs`, `OptionsMenu.cs`, `PauseMenu.cs`, `LevelCompletePopup.cs`, `PlayOptions.cs` | Standard menu canvases. |

### Recurring patterns

- **Scripts/ is flat** — 22 files in one directory, no subfolder organization. Acceptable at this scale; recommend grouping by purpose (`Player/`, `UI/`, `Audio/`, `System/`) when it grows.
- **`MonoBehaviour` everywhere.** No DI. Coroutines are used for time-based effects (transitions, tile destruction sequencing).
- **New Input System is wired through `.inputactions`.** `PlayersMovement.cs` is **auto-generated** from `PlayersMovement.inputactions` — never hand-edit it; regenerate via the Inspector. CSharpier is configured to skip this file.
- **Inspector-driven wiring.** Most cross-system references are `[SerializeField]` fields, not `FindObjectOfType` (cleaner than Lost-Scrolls in this regard).
- **Tile dissolution uses Shader Graph** (`Assets/Shaders/Tile Dissolve.shadergraph`) — animated dissolve effect when a tile is destroyed.

### What's *not* here

- No localization.
- No analytics.
- No object pooling.
- No tests, no CI.
- No save versioning — replacing the save format will break existing saves.

---

## 5. Running, building, iterating

### Open the project
1. Open Unity Hub and add `/Users/zawwintin/Desktop/Projects/Games/Soulmates`.
2. Use Editor version **2022.3.62f3** specifically.
3. Open `Assets/Scenes/Menu.unity` (or any `LevelNN.unity`) and press Play.

### Two-player input
Both players' controls are defined in `PlayersMovement.inputactions`. Verify the action map (Inspector) for current key bindings — typically WASD for one player, arrow keys for the other.

### Build
- Target is presumed Standalone (Mac/Windows). No platform-specific build scripts exist.

### Day-to-day
- **Most "code" changes happen in the Editor**, not in `.cs` files. Diffs in `.unity`, `.prefab`, `.shadergraph`, `.inputactions`, `.mixer` are real changes — review them.
- `.meta` files are required alongside every asset. Never delete a `.meta`.

---

## 6. Third-party assets

- **Kawaii Slimes** (`Assets/Kawaii Slimes/`) — sprite pack with fluffy blob characters in Blue, Green, Red, Yellow variants. Includes its own demo scenes (`AllSlime.unity`, `Animated.unity`) — those are reference scenes, not part of the game.
- **TextMesh Pro** (`Assets/TextMesh Pro/`) — Unity-bundled TMP runtime resources.

> **Recommended cleanup (later):** move both into `Assets/ThirdParty/` (via Editor) so they sort separately from your code.

---

## 7. Known issues & tech debt

| Item | Status | Notes |
|---|---|---|
| **`SaveSystem.cs` uses `BinaryFormatter`** | Open | Deprecated and unsafe in modern .NET. For a local single-player save it's not exploitable, but Unity logs a warning. Replacement: `JsonUtility.ToJson` + plain `File.WriteAllText`. Will need a save-version migration path or break existing saves. |
| **HDRP package & `HDRPDefaultResources/` folder** | Unused, kept | Project uses URP 2D. Consider removing the HDRP package from `Packages/manifest.json` and deleting the folder. |
| **Third-party packs in `Assets/` root** | Cosmetic | `Kawaii Slimes/` and `TextMesh Pro/` should ideally live under `Assets/ThirdParty/` but moving them risks breaking internal references in those packs. Move via Editor only. |
| **Flat `Scripts/` directory** | Convention gap | 22 files in one folder; group by concern (`Player/`, `UI/`, `Audio/`, `System/`) when it grows. |
| **No namespaces, no asmdef** | Convention gap | All scripts compile to `Assembly-CSharp`. Same as Lost-Scrolls — recompile times grow with codebase. |
| **No tests** | Convention gap | An EditMode test for `SaveSystem` round-trip would catch the format change in §7-1 cheaply. |
| **Auto-generated `PlayersMovement.cs` committed to git** | Acceptable | Unity Input System regenerates this on every `.inputactions` save. CSharpier skips it. Diffs may be noisy. |

---

## 8. Conventions for new code

When adding to this project:

1. **Scripts live under `Assets/Scripts/`**. Don't drop scripts in `Assets/Resources/` (build inclusion risk).
2. **One `MonoBehaviour` per file**, file name matches class name.
3. **Tunable numbers go on a `[SerializeField]` field**, not as `const`, so values can be tweaked in the Inspector.
4. **Cross-object communication:** `[SerializeField]` references wired in the Inspector. Avoid `FindObjectOfType` unless dealing with true singletons.
5. **For new player input:** edit `PlayersMovement.inputactions` (not `PlayersMovement.cs`).
6. **Time-sensitive effects** — use `Time.deltaTime` for gameplay, `Time.unscaledDeltaTime` for UI/music if you ever add a pause feature.
7. **Always create assets through the Unity Editor**, not by writing files on disk.

---

## 9. How to extend the game (recipes)

### Add a new level
1. Duplicate `Assets/Scenes/Level09.unity` → `Level10.unity` in the Project window.
2. Edit the tile layout, place both player spawn points and both goal tiles.
3. Add `Level10` to **File → Build Settings → Scenes In Build**.
4. Update the `LevelLoader` flow / level count if it's tracked anywhere.

### Add a new slime sprite variant
1. Place sprite under `Assets/Sprites/` (or `Assets/Kawaii Slimes/` if matching the existing convention).
2. Slice in the Sprite Editor.
3. Create new prefab in `Assets/Prefabs/` based on existing player prefab; swap the sprite renderer.

### Tweak feel
- Movement speed: `PlayerController` serialized fields (move duration is currently 0.35s).
- Breathing light: `PlayerLightController` min/max intensity, radius, speed.
- Tile dissolve animation: edit `Assets/Shaders/Tile Dissolve.shadergraph` in Shader Graph.
- Camera follow: `CameraController` damping/follow parameters.

---

## 10. Notes for AI assistants specifically

- **Don't move or rename files outside the Unity Editor** unless Unity is fully quit AND you also move the `.meta` file alongside the asset. Even then, anything path-loaded (`Resources.Load`, `AssetDatabase.LoadAssetAtPath`) breaks. (No `Resources.Load` calls were found in `Assets/Scripts/` — verify before any move.)
- **Don't auto-format `.unity`, `.prefab`, `.asset`, or `.mat` files** — they're YAML but Unity has specific serialization rules. Treat them as binary for editing purposes.
- **Don't edit `PlayersMovement.cs` by hand** — it's auto-generated from `PlayersMovement.inputactions`.
- **Don't `Resources.Load` from new code** — Unity bundles everything in `Assets/Resources/` into builds even if unreferenced. Use `[SerializeField]` references instead.
- **When asked to write a new feature**, outline the Editor steps the user must take (prefab setup, Inspector wiring, scene placement, build-settings update) alongside the code.
- **When the user is uncertain about a Unity concept**, give a Laravel analogy if a clean one exists.

---

*Last updated: 2026-04-25 — initial generation alongside Unity 2022.3 LTS / URP 14 migration.*
