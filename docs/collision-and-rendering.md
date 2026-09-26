# Collision layers and z-index reference

Single source of truth for both. If you add a new layer or change a z-index value, update this table in the same commit — code and this doc drifting apart is worse than not having the doc.

## Collision layers

Defined in `project.godot` under `[layer_names]`, mirrored as `private const uint Layer*` in every script that needs them (Godot doesn't expose the named layers to C# directly — the constants are the bridge, kept in sync by hand).

| Layer # | Bit value | Name | Used by |
|---|---|---|---|
| 1 | `1 << 0` = 1 | World | Walls, pillars (`Obstacle.cs`) |
| 2 | `1 << 1` = 2 | Player | `Player.cs` |
| 3 | `1 << 2` = 4 | Enemy | `EnemyBase.cs` |
| 4 | `1 << 3` = 8 | PlayerBullet | Bullets fired with `Faction.Player` |
| 5 | `1 << 4` = 16 | EnemyBullet | Bullets fired with `Faction.Enemy` |

**Layer vs. mask, concretely:**
- `Player`: layer `Player`, mask `World` (collides with walls, nothing else physically)
- `EnemyBase`: layer `Enemy`, mask `World`
- `Obstacle` (walls/pillars): layer `World`, mask `0` (doesn't need to detect anything itself)
- Player-fired `Bullet`: layer `PlayerBullet`, mask `Enemy` — can only ever hit enemies
- Enemy-fired `Bullet`: layer `EnemyBullet`, mask `Player` — can only ever hit the player
- `Pickup`: layer `0` (nothing needs to detect a pickup), mask `Player`
- `Portal`: layer `0`, mask `Player`

Note bullets currently ignore `World` entirely — they fly through walls. Real RotMG dungeons block bullets with walls; this is a known simplification (see `README.md` Next Steps), not an oversight.

## z-index

`ZIndex` set explicitly, `ZAsRelative = false` always — see `docs/godot-csharp-gotchas.md` #4 for why the default (relative, unset) is unsafe in this codebase specifically.

| Value | What | Set in |
|---|---|---|
| -100 | Floor background | `FloorBackground.cs` |
| 0 | Walls/pillars | `Obstacle.cs` |
| 0 | Player, enemies | `EntityBase.cs` (shared base, covers both) |
| 0 | Pickups | `Pickup.cs` |
| 0 | Portals | `Portal.cs` |
| 5 | Bullets | `Bullet.cs` — deliberately above everything else; bullets are the thing you most need to see clearly to dodge |

If you add a new visual layer (e.g. a particle effect, a UI world-space indicator), pick a value against this table rather than guessing — and add a row here.
