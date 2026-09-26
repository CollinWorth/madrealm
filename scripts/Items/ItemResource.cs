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
}
