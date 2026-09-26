# Art integration plan

Written ahead of actually having art, so whoever (human or AI) does the integration has a clear target instead of guessing at the approach mid-task.

## Current state: everything is a `_Draw()` primitive

No `Sprite2D`, no textures, no `.png`/`.svg` assets anywhere in the project. Every visual — player, enemies, bullets, pickups, portals, walls, the floor — is a colored shape drawn via a `_Draw()` override (`DrawCircle`, `DrawRect`, `DrawLine`) called from that node's own script. This was a deliberate placeholder choice, not an oversight: it meant zero dependency on art existing yet, and kept every scene file simpler (no texture `ext_resource` entries, no `Sprite2D` child nodes to hand-author correctly).

Where each `_Draw()` currently lives:

| Object | Script | Current visual |
|---|---|---|
| Player | `Player.cs` | White ring + blue fill circle + facing line |
| Enemies | `EnemyBase.cs` | Black ring + dark red fill circle (same for every enemy type currently — no visual distinction between Grunt/Sniper/Brute/RingCaster yet) |
| Bullets | `Bullet.cs` | White filled circle (color tracked per-shot but unused, see below) |
| Pickups | `Pickup.cs` | Colored circle (uses `ItemResource.IconColor`) + black outline |
| Portals | `Portal.cs` | Purple circle + white outline |
| Walls/pillars | `Obstacle.cs` | Flat dark gray rect/circle |
| Floor | `FloorBackground.cs` | Flat dark rect covering the room |

## How to swap in real sprites

For each object above, the pattern is the same:

1. Add a `Sprite2D` (or `AnimatedSprite2D`, if the sheet has multiple frames/animations) as a **child node** of the object in its `.tscn`, with a `Texture2D` pointing at the sprite sheet (or a `SpriteFrames` resource for `AnimatedSprite2D`, likely built from an `AtlasTexture` per frame if it's a packed sheet rather than individual files).
2. Remove (or leave dormant — see below) the corresponding `_Draw()` override in that object's script.
3. Match scale to the current collision shapes rather than the other way around — collision radii/sizes are already tuned for gameplay feel (see `docs/collision-and-rendering.md` and the current `radius`/`size` values in each `.tscn`). A sprite that's visually bigger or smaller than its hitbox is confusing to play against; resize/scale the sprite to match the existing collision geometry, don't resize collision to match sprite dimensions.
4. `EnemyBase` is one script driving every enemy type — if different enemies should look different, that's another exported field (e.g. `[Export] public Texture2D Icon;` or similar, read by the same pattern as `Stats`/`Pattern`), not a reason to fork the script. Keep the data-driven pattern (`CLAUDE.md` rule 1) intact through the art pass.

## Per-shot / per-pattern color is already wired, just inert

`BulletColor` on `BulletPatternResource` and `ProjectileColor` on `PlayerClassData` are both already tracked end-to-end through `Bullet.Fire()` into the `_color` field — they're just not currently used for rendering, because `Bullet._Draw()` was hardcoded to always draw white for testing visibility (see the comment in `Bullet.cs`). If sprite work wants per-bullet-type visuals (tinting a shared bullet sprite via `Modulate`, or picking a specific frame), the data's already there — swap `Colors.White` back for `_color` in `_Draw()`, or use `_color` to set `Modulate` on a `Sprite2D` instead.

## Animation state

Nothing currently tracks "is the player moving/idle/attacking" as explicit state — movement is read directly from input every frame with no state machine. If `AnimatedSprite2D` needs to switch animations (idle vs. walk vs. attack), that state needs to be derived (e.g. `Velocity.Length() > 0` for walking) or made explicit with a small enum — don't build a full animation state machine speculatively before it's needed, per the no-premature-abstraction rule in `CLAUDE.md`; start with the simplest derivation that covers what the sprite sheet actually supports.

## Suggested order

1. Player first (most looked-at, and the facing/aim direction already exists as `Rotation` — a directional sprite sheet can hook into that immediately).
2. Bullets (small, no directional/animation complexity, high visual impact for how little work it is).
3. Enemies (higher effort if different enemy types need different art — decide up front whether that's in scope or a "generic enemy sprite for now" pass).
4. Environment (walls/floor/pickups/portals) last — lowest gameplay-feel impact, most tiles/variety needed to look good.
