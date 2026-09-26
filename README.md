# MadRealm

A top-down bullet-hell ARPG heavily inspired by *Realm of the Mad God*:
dodge-focused combat, an 8-stat character system, data-driven enemy
bullet patterns, permadeath as the eventual target. Built in **Godot
4.7 (C#/.NET 8)**.

**Working on this codebase (human or AI)?** Read `CLAUDE.md` first --
it's the short, load-bearing rules this project follows and the
mistakes already made once that don't need repeating. Deeper reasoning
and per-system docs live in `docs/`.

## Status

One playable class (Wizard), one enemy script driven entirely by data
(4 enemy types across three scenes via different `Stats`/`Pattern`
resources, several of those patterns reused across different enemy
archetypes), object-pooled projectiles, a health HUD, an
item/inventory/pickup loop with a toggleable UI, two connected
dungeons plus the original test arena, and portals carrying player
state between scenes.
No procedural generation, equipment stat bonuses, real art, or
networking yet -- see *Next steps*.

**This has been built and played for real**, not just reviewed --
including finding and fixing several genuine bugs along the way (a
real C# compile error, invisible collision geometry, cross-branch
z-index rendering issues). See `docs/godot-csharp-gotchas.md` for the
specifics if you're extending this codebase and want to avoid repeating
them. If anything doesn't compile after a change, most likely culprits
are typos in the
`.tscn` files (hand-authored scene text) rather than the C# itself,
which I reviewed line-by-line.

## Opening it

1. Install Godot 4.7+ **with C# support** (the "Mono"/.NET build) if
   you don't have it: https://godotengine.org/download
2. Install the .NET SDK (8.0+) if you don't have it.
3. Open `project.godot` in Godot. It should prompt to build the C#
   project on first open/run -- let it.
4. Press F5 (or the Play button). `Dungeon.tscn` is the main scene
   (`Arena.tscn` is still there too -- the original flat test room,
   useful for isolated testing without walking into pillars/enemies).

## Controls

- **WASD** -- move
- **Mouse** -- aim
- **Left click (hold)** -- fire, at your class's fire rate (Dexterity-scaled)
- **I** -- toggle inventory. Click a filled slot to drop that item back
  into the world at your feet.

Controls are read directly via `Input.IsPhysicalKeyPressed` / mouse
button state rather than Godot's InputMap, to keep the first pass
simple. Rebindable controls means moving these to actual InputMap
actions (Project Settings > Input Map) -- that's a mechanical change
whenever you want it, not an architecture change.

## Architecture, and why it's shaped this way

**Everything content-ish is a `Resource`, not a script.**
`StatsResource` (the 8 RotMG stats), `PlayerClassData` (a playable
class), and `BulletPatternResource` (one enemy attack) are all
`[GlobalClass] Resource` types. A new enemy attack, a new playable
class, or a stat-boost item is a new `.tres` file authored in the
editor -- no recompiling, no new script. See `resources/` for the
starter set (Wizard, two enemy stat blocks, two bullet patterns).

**One `EnemyBase` script drives every enemy.** Behavior (idle / chase
/ attack-range) is a distance check against the player; the actual
attack is just "run the assigned `BulletPatternResource`." The two
enemies in `Arena.tscn` (Grunt, Sniper) are the *same scene* with
different `Stats` and `Pattern` resources plugged in -- that's the
whole point: variety comes from data, not from writing `GruntEnemy.cs`,
`SniperEnemy.cs`, etc. Reach for a real subclass only once an enemy
needs logic this can't express.

**Bullets are pooled, not spawned/freed.** `ObjectPool` (autoloaded as
`BulletPool`) keeps a free-list per scene and recycles instances via
`Get<T>()`/`Release()`. This genre lives or dies on having hundreds of
bullets on screen without stutter -- allocating a Node per bullet and
letting GC churn through them doesn't scale. If you pool other
bursty things later (hit-spark particles, pickups), reuse this same
class.

**`EntityBase` (Player and enemies both inherit it) owns HP and
death, and knows nothing about UI, loot, or audio.** Those all listen
for `EventBus` signals (`EntityDied`, `PlayerHealthChanged`, ...)
instead of `EntityBase` reaching out to them directly. Concretely: the
HUD's health bar has zero reference to the `Player` node -- it just
listens for `PlayerHealthChanged`. This is what lets you add a death
recap screen, a loot-drop system, or a kill-feed later without editing
`EntityBase` or `EnemyBase` at all.

**Simulation state lives on plain C# fields/properties on the Node,
separate from rendering.** `CurrentHealth`, `Velocity`, position --
none of it is UI state, none of it lives in a view/rendering class.
This isn't multiplayer-ready as-is, but it's the right shape for it:
when you do add networking, the natural cut is "server owns
EntityBase state and broadcasts it; client renders it," rather than a
rewrite. Don't build networking speculatively before you need it --
just don't undo this separation when you don't have to.

**Items are data too, and Inventory is deliberately not a Node.**
`ItemResource` follows the same `[GlobalClass] Resource` pattern as
everything else -- see `resources/items/`. `Inventory` (owned directly
by `Player`) is a plain C# class with no Godot base class at all: it's
per-player runtime state with no reason to live in the scene tree, and
staying a dumb data holder means it doesn't need Godot's signal system
to be testable in isolation later. `Pickup` and `InventoryUI` both
fire `EventBus.InventoryChanged` after touching it rather than
`Inventory` announcing its own changes -- same reasoning as everywhere
else: the thing holding data doesn't need to know who's listening.
There's no equip/stat-bonus system yet -- picking things up and
dropping them via the UI works, but nothing you're holding affects
your stats yet. That's the natural next step once you want it (see
*Next steps*).

**Collision layers** (`project.godot` -> Layer Names, mirrored as
`const uint Layer*` in each script that needs them): `World`,
`Player`, `Enemy`, `PlayerBullet`, `EnemyBullet`. Bullets only ever
collide with the *opposing* team's body layer -- player bullets can't
hit the player, enemy bullets can't hit enemies. Bullets currently
ignore `World`, i.e. they fly through walls; real RotMG dungeons block
line-of-sight with walls, so that's an early thing to add once there's
real level geometry instead of the placeholder open arena.

## Next steps, roughly in the order I'd tackle them

1. **Verify it actually runs** -- open in Godot, fix whatever typo I
   inevitably have in a hand-written `.tscn`.
2. **Equipment stat bonuses** -- the inventory loop (pick up / view /
   drop) works, but nothing you're carrying affects your character
   yet. Natural next piece: an equipped-item slot (or slots) on
   Player, and folding their bonus stats into the live `StatsResource`
   on equip/unequip.
3. **Real art** -- everything currently renders as flat-colored
   shapes via `_Draw()`. Swap for `Sprite2D`/`AnimatedSprite2D` once
   you have (or want to place) actual art; the gameplay code doesn't
   care.
4. **Procedural rooms** -- `Dungeon.tscn` is still one hand-placed
   room. RotMG's actual structure (Nexus hub + generated dungeon
   instances) is a real chunk of work; a `TileMap`-based room
   generator is the next architectural piece worth designing
   deliberately rather than bolting on.
5. **More classes/enemies/items** -- almost entirely `.tres` authoring
   at this point, per the "everything's data" section above.
6. **Permadeath + persistence** -- what happens on `PlayerDied`
   (currently just a signal nobody's listening to yet).
7. **Multiplayer** -- the big one, and genuinely a separate project
   phase. The Entity/Stats split above is prep for it, not an attempt
   at it.
