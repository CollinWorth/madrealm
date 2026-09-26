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

1. New `ItemResource` `.tres` (copy any file in `resources/items/`). Fields: `ItemName` (string), `Kind` (`ItemType` enum — `0` = Weapon, `1` = Armor, `2` = Ring, `3` = Consumable, matching declaration order in `ItemResource.cs`), `IconColor`, `Description`, optionally `Icon` (a `Texture2D` — see `docs/art-integration-plan.md` for how the existing items reference `AtlasTexture` regions of the shared item sheet; falls back to a flat `IconColor` circle if left unset).
2. Instance `scenes/items/Pickup.tscn` in a room scene, set `position` and the exported `Item` field to your new resource.

**Make it usable via the 1-8 hotkeys** by setting three more fields (all on `ItemResource`, default to "does nothing" so existing items are unaffected):

| Field | Meaning |
|---|---|
| `Effect` | `ItemEffectType` enum — `0`=None (default; gear like weapons/armor/rings should stay here), `1`=Heal, `2`=BoostMaxHP, `3`=BoostMaxMP, `4`=BoostAttack, `5`=BoostDefense, `6`=BoostSpeed, `7`=BoostDexterity, `8`=BoostVitality, `9`=BoostWisdom. Exact declaration order is in `ItemResource.cs` — check there if adding a new case, don't assume the numbers above stay stable forever. |
| `EffectAmount` | How much. For `Heal`, an instant restore to `CurrentHealth` (capped at `Stats.MaxHP`, doesn't touch the stat itself). For every `Boost*`, a **permanent** addition to that field on the player's own `StatsResource` — this is RotMG's real "stat increase potion" mechanic, not a temporary buff. |
| `ConsumedOnUse` | `true` (default) removes the item from its slot after using it, matching how potions work. Set `false` for a reusable item (untested territory — nothing in the codebase does this yet, but the field's there). |

Whoever's holding the item presses the number key matching its slot (1-8, left-to-right/top-to-bottom in the inventory grid) to trigger it — see `Player.UseItemAt()` / `Player._UnhandledInput()`. `HealthPotion.tres` (Heal) and `VitalityPotion.tres` (BoostVitality) are worked examples of both effect shapes.

**Make gear (Weapon/Armor/Ring) actually do something when worn** by setting `Bonus*` fields on `ItemResource` — one per `StatsResource` field, all defaulting to `0`:

`BonusMaxHP`, `BonusMaxMP`, `BonusAttack`, `BonusDefense`, `BonusSpeed`, `BonusDexterity`, `BonusVitality`, `BonusWisdom`.

These are **added while equipped and precisely subtracted on unequip** (`Player.ApplyGearBonus()` / `RemoveGearBonus()`) — not a permanent change like a `Boost*` potion effect. A player drags a Weapon/Armor/Ring-`Kind` item from an inventory slot onto the matching equipment slot in the character panel (`Player.EquipFromInventory()`, wired up via `InventorySlotButton`/`EquipmentSlotButton`'s drag-and-drop); whatever was equipped there before goes back into the exact inventory slot the new item came from, so equipping can never fail due to a full inventory. `IronSword.tres` (+5 Attack), `LeatherArmor.tres` (+3 Defense), and `RubyRing.tres` (+3 Wisdom) are worked examples, one per slot type.

Only one item per slot type at a time (no dual-wield, no multiple rings) — there's exactly one `Weapon`/`Armor`/`Ring` slot on `Equipment`, matching `ItemType` 1:1 minus `Consumable`. There's no explicit "unequip to empty hands" gesture yet; dragging a different item of the same `Kind` swaps it, which covers the practical need without one.

## Add a new room/scene

There's no template scene yet — copy `Dungeon.tscn` or `Catacombs.tscn` as a starting point (both are reasonably minimal: floor, walls, a handful of enemies/pickups, HUD, InventoryUI). Things every room scene needs:
- A `FloorBackground.cs`-scripted child (see `docs/collision-and-rendering.md` for the z-index reasoning)
- Outer walls using `Obstacle.cs` (`RectSize` for rects, `CircleRadius` for pillars — set exactly one)
- A `Player.tscn` instance with a `position`
- `HUD.tscn` and `InventoryUI.tscn` instances (both are self-contained via `EventBus`/`GameManager` — no per-scene wiring needed beyond instancing them)
- If you want it reachable from elsewhere, a `Portal.tscn` instance somewhere in an *existing* reachable scene pointing at your new scene's path (see `docs/scene-transitions-and-portals.md`)
