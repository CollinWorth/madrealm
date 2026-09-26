# Scene transitions and portals

## The problem this solves

`SceneTree.ChangeSceneToFile(path)` frees the entire current scene tree and loads a new one. The `Player` node — along with its `CurrentHealth`, `Stats`, and `Inventory` — is part of that tree. Without intervention, walking through a portal would silently reset the player to full health, default stats, and an empty inventory, because the "new" `Player` in the destination scene is a completely fresh instance with no relationship to the one that just got destroyed.

## The mechanism

`GameManager` (an autoload — survives scene changes, since autoloads aren't part of the scene tree that gets torn down) holds three `Pending*` fields:

```csharp
public StatsResource PendingStats { get; set; }
public int PendingHealth { get; set; } = -1;   // -1 = no pending value
public Inventory PendingInventory { get; set; }
```

`Portal.cs`, on contact with the player, populates all three from the live `Player` *before* calling `ChangeSceneToFile`:

```csharp
GameManager.Instance.PendingStats = player.Stats;
GameManager.Instance.PendingHealth = player.CurrentHealth;
GameManager.Instance.PendingInventory = player.Inventory;
GetTree().ChangeSceneToFile(DestinationScenePath);
```

The destination scene's `Player._Ready()` checks `GameManager.Instance.PendingStats != null` to detect "I just arrived via a portal" vs. "this is a fresh spawn." If arriving via portal, it adopts the pending `Stats`/`Inventory` directly (no `Duplicate()` needed — `PendingStats` is already this specific player's own previously-duplicated instance, not the shared `ClassData.BaseStats`) and overrides the post-`base._Ready()` full-heal with the carried-over `CurrentHealth`. It then **clears all three pending fields**, so a later *non-portal* scene load (e.g. just re-running the scene from the editor) doesn't pick up stale state from a previous run.

## Adding a new portal

1. Instance `scenes/world/Portal.tscn` in the source room.
2. Set `DestinationScenePath` to the target scene's `res://` path (a plain string — not a `PackedScene` export, deliberately, so scenes don't need to hold `ext_resource` references to each other just for portal wiring).
3. Position it somewhere that doesn't overlap the destination scene's own spawn point conceptually — there's no spawn-point-selection system, so the player always arrives at whatever fixed `position` the destination scene's `Player` node has. Also leave clearance from walls in the *source* scene (the portal's `CircleShape2D` radius is 24; check it doesn't overlap wall collision).

## Known limitations (not bugs — just not built yet)

- **No spawn-point selection.** Arriving via any portal into a given scene always lands at that scene's single hardcoded `Player` `position`. A multi-entrance scene would need a spawn-point system (e.g. the portal also writes "which spawn point to use" onto `GameManager`, and the destination scene reads it).
- **No loop/cycle protection.** Nothing stops two portals from pointing at each other in a way that's fine (this is normal — that's exactly what the current Dungeon ↔ Catacombs pair does) or building an unintentionally confusing web as more scenes are added. Worth a level-map doc once there are more than 2-3 connected scenes.
- **This is not a save system.** `GameManager.Pending*` only survives one scene transition — it's consumed and cleared immediately on arrival. It does not persist across closing and reopening the game. Real persistence (permadeath, session saves) is a separate, not-yet-built system — see `README.md` Next Steps.
