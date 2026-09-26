# Godot 4 C# gotchas hit in this codebase

Every entry here is a real bug that shipped to a commit in this project, not a theoretical warning. Read this before writing non-trivial new C#, especially if you can't compile-check against an actual Godot editor.

## 1. Don't name an enum the same as the property that holds it

**What happened:** `EntityBase` originally had:

```csharp
public enum Team { Player, Enemy }
public Team Team = Team.Enemy;
```

Every bare reference to `Team.Player` or `Team.Enemy` *inside a class with that instance member* failed to compile. C# resolves a simple name (`Team`) to an instance member first if one exists in scope — so `Team.Player` was parsed as "access `.Player` on the current value of the `Team` property" (a `Faction`-typed value, which has no `.Player` member), not "access the `Player` case of the `Team` enum type." `entity.Team` (qualified with an object) was always fine; only the *bare* type-style access broke.

**The fix, and the standing rule:** the enum is called `Faction`; the property is still called `Team` (typed `Faction`). Different names, zero ambiguity:

```csharp
public enum Faction { Player, Enemy }
public Faction Team = Faction.Enemy;
```

**Before adding any new enum**, check whether you're about to give it the same name as the property/field that will hold it. `ItemResource.Kind` (typed `ItemType`) already follows this correctly — different names on purpose.

**Nuance actually worth knowing, not just the rule:** this is specifically about *bare* value-expression positions. `new SomeType()`, a property's declared type, a cast target, and similar type-name-position usages are **not** ambiguous even if the type name matches a property name in the same class — the grammar for those positions resolves type names directly, unaffected by instance member shadowing. This is why `Player.Inventory` (a property) and the `Inventory` class (its type) coexisting under the same name in `Player.cs` is fine: every usage (`new Inventory()`, the property's type annotation) is in a type-name position, and the one place that reads the property (`Inventory = GameManager.Instance.PendingInventory ?? Inventory;`) is a plain assignment/read of `this.Inventory`, never `Inventory.SomeStaticMember`. If you're ever unsure which category a new usage falls into, the safe move is renaming — it costs nothing and removes the question entirely.

## 2. `IsInstanceValid` lives on `GodotObject`, not `GD`

**What happened:** `EnemyBase._PhysicsProcess` needed to check whether the cached player reference was still a live node (it can go stale if the player is freed). The original code called it unqualified — `IsInstanceValid(player)` — which was *probably* fine (it's very likely an inherited static from `GodotObject`, reachable unqualified from any derived class), but "probably" wasn't good enough without a compiler to check against, so it got hedged defensively to `GD.IsInstanceValid(player)` instead.

That hedge was wrong. The actual compiler, once the project finally built in a real Godot 4.7.2 .NET editor, produced:

```
'GD' does not contain a definition for 'IsInstanceValid' (48,35)
```

**The fix:** `GodotObject.IsInstanceValid(player)`. That's the actual home of the method — a static method on `GodotObject`, which `Node`/`Node2D`/every Godot class chain up to.

**The lesson, not just the fix:** when hedging toward "the more certain API" without a compiler available, actually verify which of two plausible APIs is correct rather than guessing between them under the assumption that hedging in *some* direction is inherently safer. Wrong-direction hedging is just a different bug. If genuinely uncertain between two APIs, say so explicitly rather than picking one confidently.

## 3. `Mathf.Max`/`Mathf.Min` with `int` arguments — resolved defensively, not confirmed

Several places needed `Max(int, int)` (clamping health, bullet counts, etc.). There was real uncertainty about whether Godot's `Mathf` class has dedicated `int` overloads or only `float`/`double` ones — if only the latter, `Mathf.Max(0, someInt)` would implicitly promote to `float` and return a `float`, which fails to compile when assigned back to an `int` without an explicit cast.

**The fix:** every int-context call was swapped to `System.Math.Max`/`System.Math.Min`, which unambiguously has `int` overloads in the .NET BCL — zero uncertainty, no dependency on Godot's specific API surface. This was never confirmed to have been a *real* bug (unlike #1 and #2, which were caught by an actual compiler) — it's a defensive fix for a plausible one. If a future change needs `Mathf`-specific behavior (e.g. `Mathf.Clamp` with a `float`), that's fine; the rule is specifically about `int` arguments where `System.Math` is available and equally correct.

## 4. z-index defaults to relative-to-parent, which silently breaks across autoload branches

Not a compile error — a runtime rendering bug, and the reason `CLAUDE.md` rule 6 exists. `CanvasItem.ZIndex` is relative to the parent's effective z-index by default (`ZAsRelative = true`). Setting a low z-index directly on a room's root node (to push a background behind everything) also drags every child of that root down with it, since children inherit the parent's contribution.

Worse: pooled bullets are parented under the `ObjectPool` **autoload**, not the room. Autoloads are added to the scene tree before the main scene loads, so bullets and room content aren't even siblings — they're in entirely separate branches under the true tree root. Default tree-order/relative-z rendering can't guarantee correct layering across that split.

**The fix, and the pattern to follow for any new custom-drawn CanvasItem:** put custom backgrounds on their own dedicated child node (see `FloorBackground.cs`, not drawn by the room root directly), and set both `ZIndex` and `ZAsRelative = false` explicitly on every gameplay-visible `_Draw()`-using node. Current values are in `docs/collision-and-rendering.md`.

## 5. Editor build variant: Standard vs. .NET/Mono are entirely separate downloads

Not a C# gotcha exactly, but cost real debugging time and is worth recording: Godot ships two separate editor builds, "Standard" (GDScript only) and ".NET"/"Mono" (adds C# support). Opening a C#-using project in the Standard build doesn't fail cleanly — it produces confusing partial errors (`No loader found for resource: ...cs (expected type: unknown)`, "format newer than supported," missing-dependency dialogs) that look like project corruption or version mismatches rather than "wrong editor entirely." If you see errors like that, the first thing to check is which editor binary is actually running — `Help > About` will confirm the version but *not* clearly whether it's the C# build; check for a `Mono`/`.NET` section under Project Settings, or that creating a new script offers a C# option, as a more reliable signal.
