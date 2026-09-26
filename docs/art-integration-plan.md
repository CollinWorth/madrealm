# Art integration plan

Originally written ahead of having any art at all. Updated now that a first real integration pass has happened -- this documents what's actually done, the one real design problem it surfaced (and how it was solved), and exactly where to pick up next.

## Assets in the project

Four sheets, dropped in and organized under `assets/sprites/`. See `assets/ATTRIBUTION.md` for license status of each -- two are unrecorded and need tracking down before any public release.

| File | Grid | Notes |
|---|---|---|
| `assets/sprites/characters/characters.png` | 16×24 cells, 12 cols × 6 rows (col 11 is a stray non-grid reference sprite, don't use it) | Converted from the original `16x24rpgdb.gif` -- see `docs/godot-csharp-gotchas.md` #5 for why (Godot doesn't import GIF). Rows 0/1 = facing down, 2/3 = facing sideways (drawn facing **left**), 4/5 = facing away/up; each pair is a 2-frame walk cycle. Columns 0-3 = "old style" recolors, 4-10 = "new style" recolors. |
| `assets/sprites/items/roguelikeitems.png` | 16×16 cells, 13 cols × 14 rows | Rings, gems, potions, weapons, armor. Row/col reference for the four already-wired icons is in the table below. |
| `assets/sprites/tiles/overworld.png` | Not yet grid-measured | Grass/sand/water/stone/wood tileset. **Not yet integrated anywhere.** |
| `assets/sprites/items/potions_overlay.png` | 160×32, appears to be 16×16 cells | Potion fill-level overlays. **Not yet integrated anywhere.** |

## What's done

**Player** (`Player.cs` + `Player.tscn`): real sprite via a `CharacterSprite` child node (`Sprite2D` + `AtlasTexture`), using column 5 ("new style" blue) of the character sheet. Scale `2.5x` to roughly match the existing 30px collision radius -- eyeballed, not pixel-measured against the collision shape, worth revisiting if it looks off in-editor.

**The one real design problem this surfaced:** this character art is drawn for 4 discrete facing directions (a classic top-down RPG sprite), not continuous rotation. The *previous* placeholder (`_Draw()` circles) rotated the whole node to point at the mouse, same as the aim-line. Doing that to a real directional sprite looks wrong -- it'd spin through arbitrary angles instead of snapping between up/down/left/right like the art expects.

**Fix:** `Player`'s root node no longer sets `Rotation` at all (it stays `0` permanently). Aim direction is tracked separately in a private `_aimAngle` float, used for bullet firing direction and for drawing the thin aim-line (still in `_Draw()`, now computed manually via `Vector2.Right.Rotated(_aimAngle)` instead of relying on node rotation). `CharacterSprite`, as a child of the unrotated root, never rotates -- instead `UpdateFacing()` picks one of 3 `AtlasTexture.Region` values (down/side/up) based on which axis of the aim vector dominates, and sets `FlipH` to mirror the single side-facing (left) pose for right-facing instead of needing a right-facing frame that doesn't exist on the sheet.

**If you add sprites to enemies later, the same problem applies** if using directional art -- check whether `EnemyBase`'s movement-direction-based rotation (if any is added) needs the same discrete-facing treatment rather than continuous rotation.

**Only the first frame of each direction pair is used** (rows 0/2/4, not 1/3/5) -- no walk-cycle animation yet, the character is static per-direction. See *Next up* below.

**Items** (`ItemResource.cs` + `Pickup.cs` + the item `.tres` files): `ItemResource` gained an `[Export] public Texture2D Icon` field, defaulting to `null`. `Pickup._Draw()` draws the icon via `DrawTextureRect` when set, falling back to the original flat-color circle when not -- so items without art assigned yet are still fully functional, just plainer. Every item now has a real icon:

| Item | Sheet region (x, y, 16, 16) | What it is |
|---|---|---|
| Iron Sword | `(0, 112)` | Plain silver sword, row 7 col 0 |
| Leather Armor | `(32, 160)` | Plain gray chestplate, row 10 col 2 |
| Ruby Ring | `(0, 32)` | Gold ring, red gem, row 2 col 0 |
| Health Potion | `(144, 80)` | Orange/red potion bottle, row 5 col 9 |
| Vitality Potion | `(96, 80)` | Clear/white potion bottle, row 5 col 6 |

Also added: a project-wide `rendering/textures/canvas_textures/default_texture_filter=0` (Nearest) setting in `project.godot`, so pixel art renders crisp at the current scale factors instead of blurry/smoothed -- applies automatically to every texture, no per-node filter setting needed.

## Next up (deferred, not forgotten)

1. **Enemy sprites.** All 4 enemy types still render as the same flat black/red circle regardless of `Stats`/`Pattern`. The character sheet has plenty of unused color variants (old-style columns 0-3, remaining new-style columns) that could differentiate Grunt/Sniper/Brute/RingCaster visually the same data-driven way `EnemyBase` already differentiates them mechanically -- likely a `[Export] public Texture2D Icon` (or an atlas region) added to... actually, since every enemy shares one script, this probably wants a small exported "which sheet region" set of fields on `EnemyBase` itself (or bundled into `BulletPatternResource`/a new small resource), not per-enemy scripts. Same rotation caveat as Player applies if reusing this character sheet.
2. **Bullets.** Currently still a plain white `_Draw()` circle. Small and fast-moving enough that pixel art might not even read better than a clean primitive -- worth a judgment call rather than assuming sprites are strictly better here.
3. **Floor/walls tileset.** `overworld.png` is organized into `assets/sprites/tiles/` but completely unused. This is real work: either a Godot `TileSet`/`TileMap` (the idiomatic way to tile a texture across a room) or manually-placed repeated `Sprite2D`s. Bigger lift than Player/items were -- budget it as its own pass, not a quick follow-on.
4. **Walk-cycle animation.** Frame B of each direction pair (rows 1/3/5) is sitting unused right next to frame A. Swapping `Player` from a static `Sprite2D` to an `AnimatedSprite2D` (or just toggling between the two `AtlasTexture` regions based on `Velocity.Length() > 0` and a timer) is a contained, well-scoped next step once it's worth the effort.
5. **`potions_overlay.png`.** Not touched yet -- would need a concrete use case (e.g. showing partial-HP/partial-charges on a consumable) before it's worth wiring in.

## Per-shot color is still wired, still inert

`BulletColor` / `ProjectileColor` are tracked end-to-end but unused for rendering (`Bullet._Draw()` hardcodes white for visibility -- see `Bullet.cs`). Unchanged by this pass. Still a one-line swap (`Colors.White` → `_color`) whenever that's wanted back.
