using Godot;
using MadRealm.Core;
using MadRealm.Items;

namespace MadRealm.UI;

// Drop target for one equipment slot (Weapon/Armor/Ring). The drag
// payload (from InventorySlotButton._GetDragData) is just the
// originating inventory slot index as an int -- accepted only if that
// slot actually holds an item whose Kind matches SlotKind, so you
// can't drop a sword onto the ring slot.
public partial class EquipmentSlotButton : Button
{
    [Export] public ItemType SlotKind = ItemType.Weapon;

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return GetDraggedItem(data) != null;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var player = GameManager.Instance?.CurrentPlayer as MadRealm.Player.Player;
        if (player == null)
            return;

        int slotIndex = data.AsInt32();
        player.EquipFromInventory(slotIndex);
    }

    private ItemResource GetDraggedItem(Variant data)
    {
        var player = GameManager.Instance?.CurrentPlayer as MadRealm.Player.Player;
        int slotIndex = data.AsInt32();
        var item = player?.Inventory.GetSlot(slotIndex);
        return item != null && item.Kind == SlotKind ? item : null;
    }
}
