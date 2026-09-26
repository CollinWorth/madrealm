# Architecture

The "why" behind MadRealm's core systems. See `CLAUDE.md` for the actionable rules this reasoning produced; this doc is for when you need to understand a decision deeply enough to extend it correctly, or to judge whether a new situation is actually an exception to a rule.

## Why data-driven Resources instead of subclassing

RotMG-style games live or die on content volume: dozens of enemy types, dozens of bullet patterns, many playable classes, hundreds of items. If each of those were a C# subclass (`GruntEnemy : EnemyBase`, `SniperEnemy : EnemyBase`, ...), the codebase would grow linearly with content, every new enemy would require a recompile, and non-programmers (or an AI working from a design doc rather than a codebase) couldn't add content without touching scripts.

Instead: `StatsResource`, `PlayerClassData`, `BulletPatternResource`, and `ItemResource` are all `[GlobalClass] Resource` types — data containers with `[Export]` fields, editable as `.tres` files directly in the Godot editor's Inspector, with zero code. `EnemyBase` is **one script** that reads a `BulletPatternResource` and a `StatsResource` and behaves accordingly. A "Grunt" and a "Brute" are the same `EnemyBase.tscn` scene with different `.tres` files plugged into its exported fields.

This was validated, not just designed: `SpreadBurst.tres` is currently used by 3 different enemies across 2 scenes; `AimedSniper.tres` is used by 3 enemies with wildly different stat blocks (fast/fragile sniper, slow/tanky brute). That's the intended payoff — the same pattern resource produces genuinely different-feeling enemies purely through the stats it's paired with.

**When to break this rule:** only when a behavior can't be expressed as data at all — e.g. an enemy with a genuinely different *algorithm* for choosing when to attack (not just different numbers/patterns). Even then, prefer adding a new exported field/mode to `EnemyBase` over a new subclass, if the new behavior is likely to be reused. A real subclass is the last resort, not the first instinct.

## Why object pooling for bullets

Bullet-hell games put hundreds of projectiles on screen simultaneously, each living ~1-3 seconds. Naively `Instantiate()`-ing and `QueueFree()`-ing a `Node` per bullet means constant allocation and GC pressure exactly during the moments (dense bullet patterns) where frame-time matters most. `ObjectPool` (`scripts/Core/ObjectPool.cs`, autoloaded as `BulletPool`) keeps a free-list per `PackedScene` and recycles instances via `Get<T>()`/`Release()` instead of destroying them.

Implementation notes:
- Keyed by `PackedScene` reference. This relies on Godot's resource loader returning the *same* C# wrapper object for repeated loads of the same `.tscn` path (true for normal `[Export] PackedScene` references set in the editor). If a future change starts loading scenes dynamically via `GD.Load<PackedScene>()` from multiple different code paths, verify this assumption still holds before relying on pool-sharing across them.
- `Release()` sets `Visible = false`, disables processing, and sets `ProcessMode = Disabled` rather than reparenting or freeing — the node stays a child of `ObjectPool` permanently once created.
- Not bullet-specific despite the current name. Reuse this for any future bursty spawn (hit-spark particles, floating damage numbers) rather than building a second pool.

## Why EventBus instead of direct references

Concretely: `HUD.cs` has never held a reference to `Player`. It subscribes to `EventBus.PlayerHealthChanged` and updates a bar. `InventoryUI.cs` doesn't hold a reference to `Pickup` or vice versa — both fire/listen to `InventoryChanged`.

The payoff is that adding a new listener (a death-recap screen, a kill-feed, an achievement system) never requires touching the system that raises the event. `EntityBase.Die()` doesn't know or care what's listening to `EntityDied` — today nothing meaningful is, and that's fine; the signal existing is what matters for extensibility, not that every signal has a consumer yet.

Practical rule: if you're about to write `GetNode<Player>(...)` or store a `Player` reference on a non-Player, non-EntityBase class, stop and check whether an `EventBus` signal would do instead. The one sanctioned exception is `GameManager.CurrentPlayer` — a single, intentional, well-known place to look up "the player" when a signal genuinely isn't the right shape (e.g. `EnemyBase` needs the player's *current position* every physics frame, which is a poll, not an event).

## Why EntityBase, and why it's multiplayer-prep (not multiplayer)

`Player` and `EnemyBase` both extend `EntityBase`, which owns `CurrentHealth`, `TakeDamage()`, `Die()`, and `Team` (a `Faction`, not literally named `Team` — see the gotchas doc for why). This means a `Bullet` can call `entity.TakeDamage(...)` on whatever it hit without knowing or caring if that's the player or a specific enemy type.

More importantly: `EntityBase`'s simulation state (`CurrentHealth`, `Velocity`, position) is plain C# fields/properties on the `Node` itself — not UI state, not something a view/renderer class owns. This is deliberately the shape you'd want if this ever becomes server-authoritative: "server owns `EntityBase` state and broadcasts it; client renders it" is a clean cut *because* state and rendering were never tangled together to begin with.

**Be precise about what this claim actually means:** no networking code exists. Nothing here has been tested against real client/server split. This is architectural posture, not a feature — the benefit is "adding networking later won't require an `EntityBase` rewrite," not "networking is half-built." Don't let this justify speculative networking code now; the rule from `CLAUDE.md` about no premature abstraction applies here too.

## Collision, rendering, and cross-branch tree structure

See `docs/collision-and-rendering.md` for the concrete layer/z-index values. The architectural point worth understanding: pooled bullets live under the `ObjectPool` **autoload**, which is a different branch of the scene tree than the room (`Dungeon`/`Arena`/`Catacombs`) entirely — autoloads are added to the tree root before the main scene loads. Godot's default 2D draw ordering (sibling tree order, z-index relative to parent) only produces predictable results *within* one branch. The moment a system spans branches — which pooling makes bullets do, permanently — implicit ordering stops being reliable and explicit absolute z-index becomes mandatory, not a nicety.

This surfaced as two real bugs (floor rendering over bullets; walls being invisible collision-only geometry) before the convention in `CLAUDE.md` rule 6 was established. Any new pooled or cross-branch-parented visual object needs to follow that convention from the start.

## Scene transitions and state

Covered in full in `docs/scene-transitions-and-portals.md`. The short version: `ChangeSceneToFile()` is destructive to the entire current tree, so anything that needs to survive a room transition (currently: player stats, health, inventory, equipment) has to be explicitly staged on an autoload (`GameManager.Pending*`) first. This is the only sanctioned mechanism — don't add a second one (e.g. a save file, a different static holder) without updating this doc and `CLAUDE.md` together.

## Two different ways items touch Stats, on purpose

`ItemResource` mutates the player's `StatsResource` through two genuinely different mechanisms, and they're not interchangeable:

- **`Effect`/`EffectAmount`** (consumables, triggered by `Player.UseItemAt()` via the 1-8 hotkeys) makes a **permanent** change. A `Boost*` effect adds to a `StatsResource` field and never subtracts — that addition is meant to last for the rest of the run, matching RotMG's real "stat increase potion" design. The item is consumed; the change isn't reversible by using another item.
- **`Bonus*`** (gear — Weapon/Armor/Ring, applied via `Player.EquipFromInventory()` / `ApplyGearBonus()` / `RemoveGearBonus()`) is **conditional on being equipped**. The exact same numbers get added on equip and subtracted on unequip/swap — precisely, not approximately, which is why `RemoveGearBonus` exists as a real mirror of `ApplyGearBonus` rather than "just don't count it anymore."

Why not one unified mechanism? Because they answer different questions. A potion's `Boost*` is "how much did this permanently add, ever" — there's no unequip step, so there's nothing to track once it's applied. Gear's `Bonus*` is "how much is currently being contributed by what's worn right now" — which requires knowing exactly what to undo the moment a slot changes. Collapsing these into one system would mean either potions need a fake "unequip" step that never fires, or equipped gear needs to track its own contribution separately from the Stats it modified anyway (reinventing the same add/subtract bookkeeping) — two real, different problems, so two mechanisms. If a third kind of stat modification shows up later (a timed buff that expires, say), it likely needs a third shape for the same reason: figure out whether it's "permanent, never reversed" or "active only while some condition holds, must be reversed precisely" before reaching for either existing pattern.

## Firing behavior lives on the weapon/pattern, not the shooter

`ItemResource` (for `Weapon`-`Kind` items) and `BulletPatternResource` (for enemy attacks) each own their entire firing behavior end to end: the properties that shape a shot (bullet count, spread, speed, color, lifetime), the cooldown timer, and the actual `Fire()`/`ReadyToFire()` methods that spawn bullets through `ObjectPool`. `Player.HandleShooting()` and `EnemyBase.HandleFiring()` are nearly identical, and deliberately thin — tick the cooldown, check readiness, call `Fire()` if the trigger's held (or the pattern says to). Neither one spawns a bullet, computes an angle, or tracks a timer itself.

This is the same instinct as *Resources over subclasses* (above), one level down: once "what does this attack look like" is fully data-driven, "when and how does it fire" belongs on that same data, not on whoever's holding/using it. Two weapons or two patterns can now behave completely differently — spread vs. single shot, fast vs. slow, big vs. small — with zero new code, the same way two enemies already do via `Stats`/`Pattern` swaps. `StatsResource.ComputeShotCooldown()` is the one deliberate seam in this split for the player: a weapon's `WeaponBaseFireCooldown` is the weapon's own number, but Dexterity scaling it down is the *shooter's* trait, not the weapon's — so the weapon calls out to the shooter's stats for that one calculation rather than owning it outright. Enemies don't have an equivalent stat, so `BulletPatternResource.FireCooldown` is used as-is.

The load-bearing consequence: `Fire()` and its cooldown are now **runtime state that lives on a `Resource`**, and Godot resources loaded from the same `.tres` path are shared, not copied. Anywhere the same weapon or pattern resource might end up assigned to more than one live wielder (equipping the same weapon twice, two enemies sharing a pattern), something has to `.Duplicate()` it first or they'll fight over one cooldown clock. See `docs/godot-csharp-gotchas.md` #6 for the mechanics and the two existing call sites (`Player.EquipFromInventory`, `EnemyBase._Ready()`) that already handle this — copy that pattern for any new place a weapon/pattern gets assigned.
