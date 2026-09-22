> **Responsive UI redesign (September 2026):** The current interface reuses Soulmates’ original cute art and illustrated how-to, with responsive layouts installed in all eleven build scenes. Open `Assets/_Project/Scenes/Menu.unity` and press Play. See [Design/DESIGN.md](Design/DESIGN.md) for the implementation, mobile controls, verification and previews. The older project notes below describe the pre-redesign layout.

# Soulmates

A two-player cooperative puzzle-platformer. Two kawaii slime characters must navigate isometric tile grids together — moving destroys tiles beneath you, stacking on the same tile triggers a heart animation, and each level requires both players to reach their respective goal tiles before falling off.

> **Deeper docs:** see [`ai_docs/CLAUDE.md`](ai_docs/CLAUDE.md) for code architecture, gameplay systems, and "how to add X" recipes.

---

## Tech stack

- **Unity 2022.3.62f3 LTS** (match this exact version to avoid asset re-serialization churn)
- **URP 14.0.12** with the 2D Renderer
- **Unity Input System 1.14.0** (new input system, not legacy `Input.GetKey`)
- **2D Animation, Pixel Perfect, PSD Importer, SpriteShape** (latest 2022 LTS versions)
- **Unity Recorder 4.0.3** (for capturing gameplay footage)

---

## Quick start

### 1. Get the project running in Unity

```bash
git clone <this repo>
```

1. Open **Unity Hub** → **Add** → select the cloned folder.
2. Use Editor version **2022.3.62f3** specifically. Unity Hub will offer to install it if missing.
3. Open `Assets/Scenes/Menu.unity` (or `Level01.unity`) and press **Play**.

> **First open after cloning is slow** (5–15 min). Unity is importing every asset and building the `Library/` cache.

### 2. Set up the C# formatter (one-time)

This project uses **[CSharpier](https://csharpier.com/)** as its code formatter — the C# equivalent of Prettier.

```bash
# 1. Install .NET SDK (macOS)
brew install --cask dotnet-sdk

# 2. From the project root, restore the pinned formatter version
dotnet tool restore
```

CSharpier is now available as `dotnet csharpier`.

---

## Daily commands

| Command | What it does |
|---|---|
| `dotnet csharpier format .` | Format every C# file in the project (vendor code + auto-generated input scripts skipped via `.csharpierignore`) |
| `dotnet csharpier check .` | Check formatting without writing — exits non-zero if anything is unformatted (CI-friendly) |
| `dotnet tool restore` | Install/restore tools pinned in `.config/dotnet-tools.json` after pulling |

### VSCode setup (recommended)

Install the **CSharpier** VSCode extension to format `.cs` files on save.

---

## Project layout

```
Assets/
├── Scripts/                  # All gameplay C# code (flat — 22 files, no subfolders)
├── Scenes/                   # Level01-09 + Menu/Demo/Credit
├── Prefabs/                  # Reusable game objects (AudioManager, GameController, ...)
├── Sprites/ Animations/ Audios/ Tiles/ Shaders/ Fonts/   # Art / audio / VFX
├── MainMixer.mixer           # Audio mixer config
├── Renderer/                 # URP renderer asset — don't move
├── Kawaii Slimes/            # ⚠ Third-party sprite pack (in Assets root)
├── TextMesh Pro/             # ⚠ TMP runtime resources (in Assets root)
└── HDRPDefaultResources/     # ⚠ Unused — slated for cleanup

ai_docs/                      # Project documentation for humans + AI assistants
ProjectSettings/              # Unity-managed — don't edit by hand
Packages/                     # Unity-managed package manifest
```

For the why behind this layout and the proposed reorganization, see [`ai_docs/CLAUDE.md`](ai_docs/CLAUDE.md).

---

## Working with Unity files (important)

Most "code" changes happen in the **Unity Editor**, not in `.cs` files. The following file types are real source files in this project — review their diffs before committing:

- `*.unity` — scenes
- `*.prefab` — prefab definitions
- `*.asset` — ScriptableObject data, render pipeline settings, audio mixer
- `*.mat` — materials
- `*.controller` — Animator state machines
- `*.shadergraph` — Shader Graph node trees
- `*.inputactions` — Unity Input System action maps

### Hard rules

1. **Never move/rename files outside the Unity Editor.** Use the Project window — it updates GUID references in scenes and prefabs. A shell `mv` will silently break everything (with one exception: moving a file together with its `.meta` while Unity is closed preserves GUIDs).
2. **Never delete a `.meta` file by hand.** Each asset has one; deleting it orphans the asset.
3. **Match the Editor version exactly.** Mixing 2022.3.62f3 with another patch version triggers re-serialization on every open.

---

## Known issues

- **`SaveSystem.cs` uses `BinaryFormatter`**, which is deprecated in modern .NET and considered insecure for untrusted data. For a single-player local save it's tolerable, but a future cleanup task is to replace it with JSON serialization (`JsonUtility.ToJson`).
- **HDRP package is installed but unused** — `Assets/HDRPDefaultResources/` and the HDRP entry in `Packages/manifest.json` should be removed once verified.
- **Auto-generated `PlayersMovement.cs`** is regenerated by Unity from `PlayersMovement.inputactions`. Don't edit it by hand; CSharpier is configured to skip it.
- **No tests, no CI** — solo project, by choice.

---

## Project status

Solo project. Currently 9 levels plus Menu, Demo, and Credit scenes. Recent work: tile drop animation, sprite sorting order on dropped tiles, breathing-light effect.

---

## Sister project

This is a sibling to [The-Lost-Scrolls](../The-Lost-Scrolls/) (2D action-platformer) by the same developer. They share the Unity 2022.3 LTS / URP 14 stack and conventions.
