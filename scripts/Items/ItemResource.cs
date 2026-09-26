using Godot;

namespace MadRealm.Items;

// Enum member is "Kind", not "Type" -- same reasoning as Faction vs.
// the Team property on EntityBase. Keeps the enum's own type name
// unambiguous everywhere it's used.
public enum ItemType
{
    Weapon,
    Armor,
    Ring,
    Consumable
}

// What using this item does, as data -- covers every RotMG-style
// potion with one enum instead of a subclass per effect. "None" is
// the default for gear (Weapon/Armor/Ring) that isn't meant to be
// used via a hotkey at all -- Player.UseItemAt() no-ops on it, so
// hitting a hotkey on a gear slot is always safe, never a crash or an
// accidental consume.
//
// Heal is instant/temporary (raises CurrentHealth, capped at
// Stats.MaxHP, and doesn't touch the Stats Resource itself). Every
// Boost* is permanent -- it mutates the player's own (already-
// per-instance-duplicated, see Player._Ready()) StatsResource
// directly, matching RotMG's real "stat increase potion" mechanic.
// Add a new Boost* case only if a new StatsResource field is ever
// added to boost -- this enum is meant to track that resource 1:1,
// not grow independently of it.
public enum ItemEffectType
{
    None,
    Heal,
    BoostMaxHP,
    BoostMaxMP,
    BoostAttack,
    BoostDefense,
    BoostSpeed,
    BoostDexterity,
    BoostVitality,
    BoostWisdom,
}

// An inventory item, as data -- same pattern as StatsResource and
// BulletPatternResource. A new item is a new .tres file (see
// resources/items/), not a new script.
[GlobalClass]
public partial class ItemResource : Resource
{
    [Export] public string ItemName = "Unknown Item";
    [Export] public ItemType Kind = ItemType.Consumable;
    [Export] public Color IconColor = Colors.White;
    [Export] public string Description = "";

    // Optional real icon (an AtlasTexture region of a shared sprite
    // sheet -- see resources/items/*.tres for examples). Null is a
    // valid, supported state: Pickup falls back to drawing IconColor
    // as a plain circle when Icon isn't set, so existing/new items
    // don't need art before they're usable.
    [Export] public Texture2D Icon;

    [Export] public ItemEffectType Effect = ItemEffectType.None;
    [Export] public int EffectAmount = 0;
    [Export] public bool ConsumedOnUse = true;
}
