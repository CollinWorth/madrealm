namespace MadRealm.Items;

// Plain C# class, not a Node/Resource -- same reasoning as Inventory:
// per-player runtime state with no reason to live in the scene tree.
// One slot per equippable ItemType (Consumable has no slot here --
// consumables go through Inventory + Player.UseItemAt instead, they
// were never meant to be worn). Dumb data holder like Inventory: it
// doesn't apply/remove stat bonuses itself or fire any signals --
// Player.EquipFromInventory() owns that, same division of
// responsibility as Inventory/Pickup already established.
public class Equipment
{
    public ItemResource Weapon { get; set; }
    public ItemResource Armor { get; set; }
    public ItemResource Ring { get; set; }

    public ItemResource GetSlot(ItemType kind) => kind switch
    {
        ItemType.Weapon => Weapon,
        ItemType.Armor => Armor,
        ItemType.Ring => Ring,
        _ => null,
    };

    public void SetSlot(ItemType kind, ItemResource item)
    {
        switch (kind)
        {
            case ItemType.Weapon:
                Weapon = item;
                break;
            case ItemType.Armor:
                Armor = item;
                break;
            case ItemType.Ring:
                Ring = item;
                break;
        }
    }
}
