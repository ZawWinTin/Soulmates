# Soulmates — Art Revamp Guide

The single reference for replacing the game's art with new (AI-generated) kawaii assets and
improving UI/UX. Use this while generating images. Companion to [`CLAUDE.md`](CLAUDE.md).

> **How we split the work:** *You* generate the PNGs from the prompts below. *Claude* handles
> all the Unity side — import settings, slicing, 9-slice borders, Animator setup, wiring assets
> into scenes, and the directional-facing code. You never touch the engine plumbing.

---

## 1. Decisions locked in

| Decision | Choice |
|---|---|
| First focus | **UI/UX** (Phase 1), characters after (Phase 2) |
| Art style | **Kawaii / cute** — soft, romantic, pastel |
| Image source | AI-generated, **you generate from the prompts here** |
| Character animation | **Option B** — generate 4 static poses; Unity does squash/stretch + breathing bob |
| Characters | One **male** (blue) + one **female** (pink) slime |

---

## 2. Tools — where to paste the prompts

| Site | Link | Use for | Free? |
|---|---|---|---|
| **Recraft** | https://www.recraft.ai | Buttons, icons, panels (has **Style lock** + transparent PNG) | ✅ daily credits |
| **Ideogram** | https://ideogram.ai | The **"SOULMATES" logo** (best at rendering text) | ✅ |
| **Leonardo** | https://leonardo.ai | **Characters** (has **Character Reference**) + backgrounds | ✅ ~150 credits/day |
| **Bing Image Creator** | https://www.bing.com/images/create | Backgrounds (DALL·E 3, fully free, but white bg) | ✅ |

**Recommended split:** Recraft for UI · Ideogram for logo · Bing/Leonardo for backgrounds ·
Leonardo for characters.

---

## 3. Consistency rules (read before generating)

AI tools drift — two generations of the "same" thing won't match. Force consistency:

1. **Generate sets in ONE image** where possible (e.g. all icons on one sheet) — they come out
   matching by construction. Claude slices them apart in Unity.
2. **Use Recraft's "Style" feature** — generate one asset you like, save it as a Style, reuse it
   for every other UI asset. Closest thing to true consistency on a free tool.
3. **Use Leonardo's "Character Reference"** for the slimes — generate the front pose first, then
   feed it back as a reference so the back view / other poses keep the same face & colors.
4. **Keep the exact palette words** (Section 4) in every prompt.
5. Expect to generate each asset **5–10 times and pick the best**. That's normal.

---

## 4. Style guide

**Theme:** soft, romantic, cozy kawaii — two soulmates finding each other.

**Palette** (paste these hex/words into prompts):

| Role | Color | Hex |
|---|---|---|
| Player 1 (boy) | pastel blue | `#8EC5FF` |
| Player 2 (girl) | blush pink | `#FFB3C6` |
| Primary accent | coral | `#FF6F91` |
| Secondary | mint | `#A8E6CF` |
| Surface / cream | warm cream | `#FFF6E9` |
| Soft accent | lavender | `#C8B6FF` |

**Buttons:** rounded "jellybean" capsules, thick soft cream outline, glossy top highlight,
subtle drop shadow.

**Motif:** hearts and sparkles everywhere; blue + pink = the two players.

**Font:** a rounded display font for titles (e.g. *Baloo 2* / *Fredoka* — free on Google Fonts);
keep Roboto for body text. (Claude will import the title font as a TextMeshPro asset.)

### Perspective — IMPORTANT (this game is 2.5D isometric)

The camera looks down at the world from a **raised ¾ isometric angle**. Two different rules:

- **World art** (characters, tiles, in-game props) → MUST be drawn at the **isometric ¾ angle**
  (seen slightly from above). A flat front-on slime will look pasted-on and wrong in the level.
- **Screen UI** (logo, buttons, icons, panels, menu background) → stays **flat / straight-on**.
  UI is overlaid on the screen, not placed in the isometric world, so no iso angle.

Every world-art prompt below already includes the isometric wording — keep it in.

---

## 5. Production order — do it in this sequence

The **logo comes last** because it includes the characters. Recommended order:

1. **UI kit** (Recraft): button → save as Style → icon sheet → panel → all under that one Style.
2. **Menu background** (Bing/Leonardo).
3. **Characters** (Leonardo): 4 poses with Character Reference.
4. **Logo** (Ideogram + Claude composites the real character sprites in) — see Phase 3.

Hand assets to Claude after each step (or all at once) — naming convention in §8.

---

## 6. Phase 1 — UI kit + background

Generate each on a **transparent background** (background image excepted) at the target size,
shape **centered**.

| # | Asset | Target size | Notes |
|---|---|---|---|
| 1 | Button (capsule) | 512×220 | Recraft — generate first, **save as a Style**. 9-slice, wide even borders. |
| 2 | Icon set (one sheet) | each icon ≈256×256 | Recraft, **same Style**. play, pause, gear, music, sound, replay, next, home, lock, heart. |
| 3 | Panel / popup frame | 512×512 | Recraft, **same Style**. 9-slice, wide uniform borders, transparent center. |
| 4 | Menu background | 1920×1080 | Bing/Leonardo. Opaque is fine. |

### Prompts

**1 — Button capsule** (Recraft — **save as Style!**)
```
Kawaii UI button, rounded jellybean capsule shape, glossy pastel pink surface,
thick soft cream-white outline, subtle inner highlight at top, soft drop shadow,
clean vector cartoon style, transparent background, centered, empty no text
```

**2 — Icon set, one sheet** (Recraft, same Style)
```
Set of kawaii cute game UI icons on a grid, consistent style: play triangle,
pause, gear settings, music note, speaker sound, replay arrow, next arrow,
home, padlock, heart. Soft rounded chunky shapes, white with thick outline and
pastel fill, glossy, transparent background, flat vector mobile game icons,
uniform size, evenly spaced
```

**3 — Panel / popup frame** (Recraft, same Style)
```
Kawaii rounded UI panel frame, soft cream rounded-rectangle card with thick
pastel-pink border, cute heart corner accents, subtle paper texture, soft drop
shadow, transparent center and background, mobile game popup window, wide
uniform borders for 9-slice
```

**4 — Menu background** (Bing / Leonardo)
```
Soft dreamy kawaii game background, pastel sky gradient pink to lavender,
fluffy rounded clouds, distant rolling mint-green hills, floating hearts and
sparkles, cozy storybook style, soft lighting, no characters, no text,
16:9 1920x1080, gentle bokeh, flat illustration
```

### UX improvements Claude will make (beyond new art)
- Add the **title/logo** to the main menu (none today).
- Unify button + icon styling (currently glossy buttons clash with flat white icons).
- Establish visual hierarchy: **Play** dominant, Options/Quit secondary.
- Add hover/press feedback (scale + tint) to all buttons.
- Cuter level-select (heart-gated locked/unlocked nodes).

---

## 6. Phase 2 — Characters (Option B)

Generate **4 static poses total**, transparent background, character **centered**, same canvas
size (**512×512**). Unity does all the animation (squash on land, stretch on hop, gentle
breathing bob) — no extra frames needed.

| Pose | Tool |
|---|---|
| Male slime — front | Leonardo |
| Male slime — back (Character Reference on) | Leonardo |
| Female slime — front | Leonardo |
| Female slime — back (Character Reference on) | Leonardo |

> Left/right facing is **free** — Claude flips the sprite horizontally in code.

### Prompts

> All four use the **isometric ¾ angle** so they sit correctly in the 2.5D world.

**Male — front idle**
```
Cute kawaii slime character, chubby rounded blue jelly body, glossy soft
shading, big sparkly happy eyes, small rosy cheeks, tiny boyish tuft on top,
isometric 2.5D game perspective seen from a slightly raised 3/4 angle, facing
down-screen toward the camera (front), friendly smile, pastel blue with cream
highlights, thick soft outline, full body, centered, transparent background,
mobile game character sprite, clean vector cartoon, soft drop shadow
```

**Male — back** (Character Reference = the front image)
```
The same blue kawaii slime, isometric 2.5D game perspective from a slightly
raised 3/4 angle, viewed from BEHIND facing away up-screen, no face visible,
back of the rounded jelly body and the little tuft on top, same colors and
style, centered, transparent background
```

**Female — front idle**
```
Cute kawaii slime character, chubby rounded pink jelly body, glossy soft
shading, big sparkly happy eyes with long lashes, rosy cheeks, a tiny bow or
flower on top, isometric 2.5D game perspective seen from a slightly raised 3/4
angle, facing down-screen toward the camera (front), sweet smile, pastel pink
with cream highlights, thick soft outline, full body, centered, transparent
background, mobile game character sprite, clean vector cartoon, soft drop shadow
```

**Female — back** (Character Reference = the front image)
```
The same pink kawaii slime, isometric 2.5D game perspective from a slightly
raised 3/4 angle, viewed from BEHIND facing away up-screen, no face visible,
back of the rounded jelly body with the little bow/flower on top, same colors
and style, centered, transparent background
```

### Why we need front AND back — the facing fix

Movement is isometric; each key moves along a screen diagonal:

| Key | Screen direction | Correct view |
|---|---|---|
| **D** (right) | up-right → away from camera | **back**, facing right |
| **W** (up) | up-left → away from camera | **back**, facing left |
| **S** (down) | down-right → toward camera | **front**, facing right |
| **A** (left) | down-left → toward camera | **front**, facing left |

Today the blob only has a **front** sprite and just flips left/right, so pressing **W or D**
(moving away) shows its face — the "weird facing" bug. With a **back** view added, Claude swaps
front/back per direction (and keeps flipX for left/right) so all 4 directions look right.
See `Assets/_Project/Scripts/PlayerController.cs` (`CharacterMove`).

---

## 7. Phase 3 — Logo (LAST — after characters)

The logo is generated last so it can include the two slimes. **Recommended approach:** generate
only the **wordmark text** in Ideogram, and let Claude **composite the real character sprites**
(from Phase 2) into the title inside Unity. That guarantees the slimes in the logo are *exactly*
the game characters — AI redrawing them inside the logo would never match.

**Logo wordmark only** (Ideogram — leave space for characters on each side)
```
Cute kawaii game logo wordmark "SOULMATES", soft rounded bubbly 3D letters,
pastel pink and mint gradient with glossy highlights, thick cream outline,
a small heart replacing an accent, sparkles, soft drop shadow, transparent
background, mobile game title art, centered, empty space on the left and right
```

Then Claude places the **boy slime** on one side and the **girl slime** on the other, peeking
behind the letters — a Canvas group, so it can even animate (gentle bob).

*Alternative (one baked image):* if you'd rather a single all-in-one PNG, add to the prompt:
`two chibi slimes one blue boy and one pink girl peeking from behind the letters` — but they
won't match the in-game characters. The composite approach above is preferred.

---

## 8. Handoff checklist

When you've generated assets:
1. Drop the PNGs into `Assets/_Project/Sprites/` (UI) — keep clear names
   (`ui_button.png`, `icon_play.png`, `char_boy_front.png`, …).
2. Tell Claude which files are new.
3. Claude sets import settings (Sprite mode, pivot, pixels-per-unit, 9-slice borders),
   slices the icon sheet, wires them into the Menu/Pause/popup canvases, imports the title font,
   and writes the front/back facing code.

> **Never** `mv`/rename or delete `.meta` files outside the Unity Editor — it breaks GUID
> references (see `CLAUDE.md` §"Working with Unity files"). Adding brand-new PNGs is safe.
