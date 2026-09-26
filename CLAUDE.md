# MadRealm — AI/dev guide

A top-down bullet-hell ARPG heavily inspired by *Realm of the Mad God*, built in **Godot 4.7 (C#/.NET 8)**. This file is the operating manual: the rules this codebase follows, the mistakes already made and fixed once (don't repeat them), and where to look for deeper reasoning. Read `README.md` for what the game *is*; read this for how to *work on it* correctly.

**If you're an AI picking this up cold: read this whole file before touching code.** It's short on purpose. Every rule below exists because skipping it caused a real bug earlier in this project's history — they're not style preferences.

## Quick facts

- Engine: Godot **4.7.x**, C#/.NET 8, `TargetFramework net8.0`
- Default scene: `scenes/world/Dungeon.tscn` (there's also `Arena.tscn`, a minimal test room, and `Catacombs.tscn`, a second dungeon connected via portal)
- Build/run: open `project.godot` in the **.NET/Mono** build of Godot specifically — the standard build cannot load `.cs` scripts at all and will fail in confusing ways (see `docs/godot-csharp-gotchas.md`)
- Controls: WASD move, mouse aim, hold left-click to fire, `I` toggles inventory

## Doc map

- `README.md` — what's built, controls, high-level architecture, roadmap. Start here as a human.
- `docs/architecture.md` — the full "why is it built this way" for the core systems
- `docs/godot-csharp-gotchas.md` — **read before writing new C#**. Specific traps already hit in this codebase, with the actual compiler errors they produced
- `docs/collision-and-rendering.md` — the collision layer bitmask table and z-index convention, so you don't have to re-derive or guess either
- `docs/scene-transitions-and-portals.md` — how player state survives a scene change (it doesn't, by default — this is the fix)
- `docs/content-authoring-guide.md` — how to add a new class/enemy/item/pattern (almost always: new `.tres` file, not new code)
- `docs/art-integration-plan.md` — where sprite work plugs in when it's ready (nothing renders as real art yet, everything is `_Draw()` primitives)

## The rules

These are load-bearing, not stylistic. Violating one of these is how the bugs in `docs/godot-csharp-gotchas.md` happened.

1. **Content is data, not code.** A new enemy, playable class, bullet pattern, or item is a new `.tres` `Resource` file (`StatsResource`, `PlayerClassData`, `BulletPatternResource`, `ItemResource`). Do not write a new script (`GruntEnemy.cs`, `FireballItem.cs`, etc.) unless the behavior genuinely cannot be expressed as data through the existing `EnemyBase`/`Pickup`/etc. scripts. If you think you need a new script for "just one more enemy type," you're almost certainly wrong — check `docs/content-authoring-guide.md` first.

2. **Everything that can take damage and die inherits `EntityBase`.** HP, `TakeDamage`, `Die` logic lives there exactly once. `Player` and `EnemyBase` both extend it. Don't duplicate health-tracking logic on a new class — extend `EntityBase` instead.

3. **Anything spawned in bursts goes through `ObjectPool`** (autoloaded as `BulletPool`), never raw `Instantiate()`/`QueueFree()`. This exists specifically because bullet-hell spawns hundreds of short-lived nodes per second; skipping the pool reintroduces the GC churn it was built to avoid. Currently used for bullets; reuse it for anything else spawned in bulk (hit-spark particles, etc.) rather than building a second pooling mechanism.

4. **Cross-system communication goes through `EventBus` signals, not direct references.** UI, audio, and loot systems should never hold a reference to `Player` or reach into `EnemyBase`. Emit a signal (`PlayerHealthChanged`, `EntityDied`, `InventoryChanged`, ...); let listeners subscribe. Check `EventBus.cs` before adding a new cross-system notification — extend it there rather than inventing a parallel mechanism.

5. **`GameManager.Pending*` is the only sanctioned way to carry player state across a scene change.** `ChangeSceneToFile()` destroys the entire current scene tree, `Player` included — nothing survives unless it's explicitly saved somewhere that isn't part of the scene being destroyed (i.e., an autoload). See `docs/scene-transitions-and-portals.md` before adding a second way to do this.

6. **z-index is always explicit and absolute** (`ZIndex = <value>; ZAsRelative = false;`) on anything with a custom `_Draw()`. Godot's default relative-to-parent z-index breaks silently the moment something lives under a different tree branch than you expect — which pooled bullets always do, since they're parented under the `ObjectPool` autoload, not the room. See `docs/collision-and-rendering.md` for the current layer values; don't invent a new implicit-ordering scheme.

7. **Never name a Resource's enum type the same as the property that holds it.** `public Faction Team` (not `public Team Team`) — a bare `Team.Player` inside a class that also has an instance member called `Team` resolves to the member first and fails to compile. Full story and a second example in `docs/godot-csharp-gotchas.md`. Check any new enum you add against this before naming it.

8. **Behavior lives on the data/resource that owns the relevant properties, not on the entity using it.** If a `Resource` exports the numbers that shape an attack (fire rate, projectile count, spread, speed), that same `Resource` should expose the `ReadyToFire()`/`Fire()` that acts on them — the entity wielding it should only ever tick a cooldown and ask, never reach in and spawn bullets itself. See `ItemResource.Fire()`/`ReadyToFire()` (weapons; `Player.HandleShooting` just ticks and asks) and `BulletPatternResource.Fire()`/`ReadyToFire()` (enemy attacks; `EnemyBase.HandleFiring` does the same). This is what lets two weapons or two patterns behave completely differently through data alone, with zero new code. One consequence to watch for: once a shared `.tres` starts carrying live per-instance state this way (a cooldown timer), every place that resource gets assigned needs to `.Duplicate()` it first, or multiple wielders will share one clock — see `Player.EquipFromInventory` and `EnemyBase._Ready()` for the fix, and `docs/godot-csharp-gotchas.md` for why Godot shares `Resource` instances loaded from the same path in the first place.

## Verification reality check

This project has been developed across two environments: a Linux box with no Godot or .NET SDK installed (where C# gets hand-reviewed line-by-line but never compiled), and the actual Godot editor on macOS (where it finally gets built for real). **Assume any AI-authored C# in this repo's history was reviewed carefully but not necessarily compiled before being committed.** Real compile errors have already slipped through review twice (`GD.IsInstanceValid` doesn't exist — it's `GodotObject.IsInstanceValid`; uncertainty about `Mathf.Max`'s int overloads led to a defensive `System.Math.Max` swap instead). Both are documented in `docs/godot-csharp-gotchas.md` so they don't recur.

If you're an AI without a way to build the project: say so explicitly, review extra carefully against the gotchas doc, and prefer the more-certain API (`System.Math` over uncertain `Mathf` overloads, fully-qualified static calls over assumed inheritance) when there's any doubt. If you're working with build access (human or AI): **actually build it.** Don't rely on review alone when a compiler is available — that's strictly better information.

## Coding standards

- **Comments explain WHY, never WHAT.** Identifiers should make the what obvious. A comment earns its place by recording a non-obvious constraint, a hidden gotcha, or the reasoning behind a choice a reader would otherwise question. Every comment in this codebase should survive the test "would removing this confuse a future reader" — if not, cut it.
- **No speculative abstraction.** Don't build a system more general than the current need (see: `EnemyBase` handling every enemy via data instead of a class hierarchy — that generality was earned by the actual requirement of many enemy variants, not added preemptively). Three similar lines beat a premature abstraction.
- **Resources over subclasses, always the first instinct** for anything that's fundamentally "a variant of the same behavior with different numbers" (stats, patterns, items, classes).
- **`partial class` on every Godot script class** — required by Godot's C# source generator for `[Export]`/`[Signal]` support; a missing `partial` is a silent-until-runtime footgun in some Godot versions, so just always include it.
- Full architectural rationale (pooling, EventBus, Entity/Stats split, why the split matters for eventual multiplayer) lives in `docs/architecture.md` — this section is the "what to do," that doc is the "why."

## Directory map

```
scripts/
  Core/        EventBus, GameManager, ObjectPool — the three autoloads
  Entities/    EntityBase (HP/death), StatsResource (the 8 stats)
  Player/      Player, PlayerClassData
  Enemies/     EnemyBase, BulletPatternResource
  Projectiles/ Bullet
  Items/       ItemResource, Inventory (plain C# class, not a Node), Pickup
  UI/          HUD, InventoryUI
  World/       FloorBackground, Obstacle, Portal
scenes/        Mirrors scripts/ by feature, plus world/ (rooms) and items/ (Pickup.tscn)
resources/     .tres data files — stats/, classes/, patterns/, items/
docs/          Deeper per-system documentation (see Doc map above)
```

## Adding content (short version)

New enemy: new `StatsResource` `.tres` + new/reused `BulletPatternResource` `.tres`, instance `EnemyBase.tscn` in a room scene, set `Stats`/`Pattern` overrides. No code.
New playable class: new `StatsResource` + new `PlayerClassData` `.tres`. No code.
New item: new `ItemResource` `.tres`, place a `Pickup.tscn` instance somewhere with `Item` set. No code.
Full walkthrough with exact property names: `docs/content-authoring-guide.md`.

## Roadmap

Kept in one place to avoid drift: see **Next steps** in `README.md`. Don't duplicate it here — update that section when priorities change.
