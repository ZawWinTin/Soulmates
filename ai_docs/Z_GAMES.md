# Soulmates on z-games (WebGL build)

Status: LIVE (since 2026-07-27).

Soulmates is the first game on **z-games**, Zaw's games site (`~/Developer/Projects/Websites/Herd/z-games`).
The general checklist every hosted game follows is **z-games `ai_docs/specs/hosting-games.md`**; the Unity
details (embedding, bridge) are **z-games `ai_docs/specs/unity-integration.md`**. This file holds what is
specific to Soulmates.

## Facts

| | |
|---|---|
| Slug | `soulmates` (never changes) |
| Engine | Unity 2022.3.62f3, URP 2D, WebGL |
| z-games catalog | `kind: adventure`, `access: free` (Zaw, 2026-10-15: Soulmates stays free, no trial), `delivery: browser`, `engine: unity`, `minWidth 800 / minHeight 600`, `orientation: landscape` |
| Build output | `z-games/public/_builds/soulmates/Build/soulmates.{loader.js,data,framework.js,wasm}` |
| Build tool | `Assets/_Project/Scripts/Editor/WebGLBuildSetup.cs` |
| Bridge | `Assets/_Project/Scripts/Web/GameBridge.cs` (self-spawning `GameBridge` object) + `Assets/Plugins/WebGL/ZGamesBridge.jslib` |

## Build

**Zaw runs it in the editor:** Tools ▸ Soulmates ▸ **WebGL: 1. Configure For z-games**, then **WebGL: 2. Build
For z-games** (about 13 min). It writes straight into the z-games folder above.

An agent may build headless **only with the editor closed** (same rule as `tools/compile-check.sh`):

```
U=/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity
"$U" -batchmode -nographics -quit -projectPath . -executeMethod WebGLBuildSetup.Build -logFile /tmp/soulmates-webgl.log
```

Never trigger a compile or build through the Unity MCP (it wedged the editor for hours).

Then the checks in hosting-games §5 and commit the new build in z-games ("fix: rebuild Soulmates (…)").
**Never push.**

## Bridge (what Soulmates implements)

| Message | Where |
|---|---|
| `Ready` | `GameBridge` on boot (jslib `ZGamesNotifyReady`) |
| `SetContext` | `GameBridge.SetContext` — stores `IsGuest` / `UserName` |
| `LoadData` | `GameBridge.LoadData` — raise-only merge of `{"level":N,"stars":[…]}`; pushes local-ahead progress back up |
| `SaveData` | `GameBridge.NotifyProgressSaved` (hooked at `SaveSystem.Write`) |
| `SetDevice` | `GameBridge.SetDevice` — `IsTouch` / `IsPortrait` + `DeviceChanged` |
| `exit` | jslib `ZGamesNotifyExit`, from the menu's Exit |
| `SetAccess` / `PurchaseRequired` | not implemented — not needed while the game is free |

## Download size

54 MB on disk since 2026-11-11 (was 82 MB): the HDRP package is gone and the 4 large Garden textures are
compressed. Vercel already serves `.wasm`/`.data` with Brotli on the fly, so a player downloads about **29 MB**
(was ~46 MB). A Unity Brotli build would save only a few MB more, so compression stays Disabled. Details in
z-games `ai_docs/specs/deployment.md`.
