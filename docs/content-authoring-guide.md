# Content authoring guide

Practical walkthroughs for adding content. All of these are "new `.tres` file(s), no code" — see `docs/architecture.md` for why that's the design, and `CLAUDE.md` rule 1 for when it's actually okay to deviate.

Every example below is a real, working file already in the repo — copy the closest match and edit values rather than starting from a blank resource, it's less error-prone than hand-typing the `.tres` format from scratch.

## Add a new enemy

1. **Stats**: copy an existing file in `resources/stats/` (e.g. `GruntStats.tres`) to a new name, edit the 8 fields (`MaxHP`, `MaxMP`, `Attack`, `Defense`, `Speed`, `Dexterity`, `Vitality`, `Wisdom`). `MaxMP` is unused by enemies currently (no enemy abilities cost mana yet) — leave it 0.
2. **Attack pattern**: reuse an existing one in `resources/patterns/` if it fits, or make a new `BulletPatternResource` `.tres` (see *Add a new bullet pattern* below).
3. In a room scene (`Dungeon.tscn`, `Catacombs.tscn`, or a new one), instance `scenes/enemies/EnemyBase.tscn`, set `position`, and override the exported `Stats` and `Pattern` fields to point at your new/chosen resources:
   ```
   [node name="MyNewEnemy" parent="." instance=ExtResource("...")]
   position = Vector2(x, y)
   Stats = ExtResource("...")
   Pattern = ExtResource("...")
   ```
4. Optional tuning fields also exported on `EnemyBase` (defaults are usually fine): `AggroRange` (300), `AttackRange` (220), `FireCooldown` (1.5 seconds).

No script needed. If you find yourself wanting an enemy that, say, only fires while stationary, or has two alternating patterns — that's a real behavior gap in `EnemyBase`, worth extending the shared script with a new exported flag rather than subclassing (per `CLAUDE.md` rule 1).

## Add a new bullet pattern

Copy `resources/patterns/SpreadBurst.tres` or `AimedSniper.tres`. Fields on `BulletPatternResource`:

| Field | Meaning |
|---|---|
| `BulletCount` | Bullets fired per volley |
| `SpreadDegrees` | Total arc the volley covers. `0` = single aimed shot. `360` = full ring (see note below) |
| `BulletSpeed` | px/sec |
| `BulletLifetime` | Seconds before a bullet despawns if it hasn't hit anything |
| `AimAtPlayer` | `true` = volley centers on the player's current position; `false` = centers on the enemy's own facing (`Rotation`) |
| `BulletColor` | Currently unused for rendering — all bullets render white regardless (see `Bullet.cs` `_Draw()`), but still tracked per-shot so reverting to colored bullets is a one-line change, not a data migration. Set it meaningfully anyway. |

**360-degree rings work correctly** — the angle-distribution math centers each bullet within its own equal slice of the total spread rather than spacing by `count - 1`, specifically so a full circle doesn't put two bullets on top of each other at the seam. See `EnemyBase.FirePattern()` if you need to understand the math itself.

## Add a new playable class

1. New `StatsResource` `.tres` (base stats for the class).
2. New `PlayerClassData` `.tres` (copy `resources/classes/Wizard.tres`), pointing `BaseStats` at your new stats resource, with `ProjectileColor`/`ProjectileSpeed`/`ProjectileLifetime` set (color is currently unused for the same reason as bullet patterns above — bullets always render white — but keep it meaningful).
3. In `scenes/player/Player.tscn`, change the `ClassData` export to point at your new resource — **note this currently means only one class can be "active" at a time**, since there's no class-select screen yet. Swapping classes means editing this one reference. A real class-select system is future work, not yet built.

## Add a new item

1. New `ItemResource` `.tres` (copy any file in `resources/items/`). Fields: `ItemName` (string), `Kind` (`ItemType` enum — `0` = Weapon, `1` = Armor, `2` = Ring, `3` = Consumable, matching declaration order in `ItemResource.cs`), `IconColor`, `Description`.
2. Instance `scenes/items/Pickup.tscn` in a room scene, set `position` and the exported `Item` field to your new resource.

**Items currently do nothing when picked up beyond occupying an inventory slot.** No equip system, no stat bonuses. That's the next real system to build (see `README.md` Next Steps) — don't assume `ItemResource` fields imply mechanical effects that don't exist yet.

## Add a new room/scene

There's no template scene yet — copy `Dungeon.tscn` or `Catacombs.tscn` as a starting point (both are reasonably minimal: floor, walls, a handful of enemies/pickups, HUD, InventoryUI). Things every room scene needs:
- A `FloorBackground.cs`-scripted child (see `docs/collision-and-rendering.md` for the z-index reasoning)
- Outer walls using `Obstacle.cs` (`RectSize` for rects, `CircleRadius` for pillars — set exactly one)
- A `Player.tscn` instance with a `position`
- `HUD.tscn` and `InventoryUI.tscn` instances (both are self-contained via `EventBus`/`GameManager` — no per-scene wiring needed beyond instancing them)
- If you want it reachable from elsewhere, a `Portal.tscn` instance somewhere in an *existing* reachable scene pointing at your new scene's path (see `docs/scene-transitions-and-portals.md`)
