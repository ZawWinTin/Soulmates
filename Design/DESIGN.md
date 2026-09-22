# Soulmates — Cute responsive UI

A complete interface redesign on `design/soulmates-mobile-redesign`.

## Direction

Restore Soulmates' original cute identity: puffy pastel logo, cloud scenery, glossy candy buttons, blue and pink slimes, cream panels with heart corners, and little floating islands. The how-to uses a newly generated cohesive slime-and-islands illustration with separate readable control keys. Touch mode uses directional arrows for both characters.

## Screens and interactions

- Home: original illustrated logo, centered playful actions, pastel scenery; landscape phones rearrange the logo and controls to fit.
- Level selection: nine islands along a winding trail, numbers embossed directly on the island surfaces, saved star ratings, and the current slime marker. The entire island is clickable; no circular number badge.
- Gameplay: board fits between the HUD and controls, including safe areas. Desktop shows clickable W/ASD and arrow-key clusters in their familiar keyboard arrangement; phones show two isometric direction pads.
- Mobile: independent blue/pink pads, simultaneous pointers, repeat-on-hold, and geometric direction icons.
- Help: generated glossy slime illustration and dedicated blue/pink control groups, with a short shared objective.
- Pause/settings/results: candy buttons and the original heart-framed panel; pause ownership, audio settings and current-run star ratings remain functional.

## Implementation

`GardenInterface` creates the uGUI layout from a serialized `GardenTheme`. `GardenDesignSetup.Install` uses Unity's asset and scene APIs to install the presentation into all eleven build scenes. Old canvases remain inactive for reference; their UI input and pause components are disabled. The new UI owns transitions, while the existing game controller owns win/loss/save logic.

Touch/portrait flags retain the existing GameBridge contract. z-games has a matching branch with a viewport-sized touch player specifically for Soulmates. z-core needs no changes.

## Art

New built-in imagegen assets: `Assets/_Project/Garden/tutorial-v2.png` and `marshmallow-button-v2.png`. Exact generation prompts are in `Design/ART-PROMPTS.md`. Button padding is trimmed through Unity sprite metadata; a nine-slice preserves the end caps at different sizes. Square control keys use square geometry to avoid squeezing a pill into a key.

Reuses the project's original UI and character sprites under `Assets/_Project/Sprites`, including `ui_logo_3.png`, `bg_menu.png`, `ui_panel.png`, `ui_btn.png`, `key_cap.png` and `tile_grass_1.png`. The earlier generated garden illustration is an unused concept; it is not the active theme.

The internal `Garden*` class names remain to preserve serialized references. Clean font materials avoid unwanted inherited shadows without changing the source font assets.

## Review

`Design/Preview/Soulmates.app` is a local development build (ignored by Git). `GardenReviewCapture` is opt-in in development builds with `--garden-review <absolute-output-folder>`. It captures desktop, portrait and landscape screens and exercises movement guards and touch input. It never runs in release builds.

Phone-sized desktop renders verify layout, not physical Safari/Chrome multitouch, thermal performance, browser fullscreen, or interrupted cloud requests. Those still require real-device testing.

## Local browser preview

The matching z-games branch uses an immersive touch layout with safe-area padding. Its previous build is backed up at `Design/Backups/soulmates-before-garden` (ignored by Git). `GardenDesignSetup.BuildWebPreview` produces the replacement under `Design/WebGL/soulmates`; the four files in `Build` are copied to z-games' `public/_builds/soulmates/Build` only after a successful build. Nothing is published remotely.

Validation: native development build, WebGL release build, captures at 1200×800 / 390×844 / 844×390, all nine level layouts, dual touch movement, invalid diagonal input, and pause input gating. z-games passes TypeScript and the changed player's ESLint check.

## Fall and retry timing

GameOver locks both characters’ movement and allows the fall animation and audio to run for 1.15 seconds. A 0.45-second fade then reloads the same level. An input shield prevents opening a menu mid-retry. The opt-in review checks that falling does not reload immediately, the partner is locked, and the same level reloads afterward.

Keyboard QA drives the real Input System bindings for both players and verifies that a valid key initiates movement. Arrow icons scale to their containing key and use geometry, independent of font coverage.

## Restored atmosphere and rewards

Gameplay keeps the original `bg_gradient.png` sky and drifting cloud sprites. The camera fit now resizes the sky and cloud bounds whenever the viewport changes. Win ratings use the original glossy `star_3d.png` with the `WinStars` staggered bounce-and-flash reveal, showing only the current run’s earned stars.

## Interaction refinement

The current island gently floats; unlocked islands lift on hover and squash slightly on press. Reduced motion disables the idle/hover movement. Ghost star slots distinguish unearned rewards. Win stars now correspond to completion, time, and the collectible individually, with labels explaining each goal; a missed time star stays empty even when the collectible is earned. Cloud sizes are normalized across sprite variants and drift at a calmer parallax speed.

## Retry notice

The retry message uses a cream bubble with a pink rim, heart icon, and a two-line heading and message in the reserved HUD band. Its short pop-in respects reduced motion, and the bubble is rebuilt when the viewport changes. The targeted native review checks desktop, portrait, and landscape text fit and same-level restart.

## Characters and rune arrival

Pink’s new front/blink sprite sheet uses softer jelly shading and simplified eyes while retaining her bow and silhouette. Unity imports the two aligned expressions as sub-sprites; the original character art remains available.

Movement keeps the 0.35-second duration, adding brief anticipation, an eased hop, and a non-blocking landing settle. Valid movement interrupts an idle backflip and restores its grounded origin before hopping. Idle breathing is subtler. An arriving character bounces once and waits contentedly; both characters celebrate together for 1.15 seconds before results appear (0.4 seconds with reduced motion). Scoring is recorded at arrival, before this presentation delay.

The original Norse rune carvings remain intact. RuneAwakening narrows and softens the surrounding light to preserve their contrast, adds an arrival pulse, and holds a brighter settled glow. Reduced motion suppresses the pulse, particle burst, idle flip, and decorative hop motion.

## Gameplay review fixes

Gameplay has a direct Quit action: it leaves the web player through the existing bridge, quits a native build, and stops Play mode in the Unity Editor. Separate Time star and Bonus star indicators replace the thin progress bar; the time goal counts down, then encourages continuing after the time star is missed. The generated Soulmates emblem is used in the retry bubble and Unity application icon settings. GardenArrow explicitly requires CanvasRenderer, including in the Editor; visual QA checks the resulting mesh as well as text overflow.

## Candy timer and SVG icons

The time-star meter again uses the original overlapping gold star badge, rounded draining gold bar, a moving gold/white handle, and warm sparkles (disabled with reduced motion). The numeric countdown and separate collectible-star status remain available in responsive layouts. Pause includes a dedicated Quit game button.

All symbolic icons in the active Garden interface now come from SVG sources (`arrow-up.svg`, `help.svg`, `pause.svg`), exported to 128px PNG sprites for uGUI. Directional icons rotate around their centers. Back/next buttons use the same arrow asset; no font arrows, emoji, question-mark text buttons or text pause bars are rendered. WASD remains text because it identifies keyboard keys. Inactive legacy canvases are retained as design references.

Latest refinement: Quit lives in the pause menu only; gameplay header contains Help and Pause. Help uses a standalone SVG question mark without an inner circle. SVG movement arrows rotate about their centers; navigation button labels and arrows form a centered group with a fixed gap.
