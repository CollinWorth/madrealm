using Godot;
using MadRealm.Core;

namespace MadRealm.UI;

// A single inventory-grid slot. This has to be its own script rather
// than InventoryUI just managing plain Button nodes, because Godot's
// drag-and-drop virtual methods (_GetDragData etc.) are per-Control
// overrides -- there's no way to add drag support from a "manager"
// script sitting above a generic Button. EquipmentSlotButton is the
// matching drop-target half of this.
public partial class InventorySlotButton : Button
{
    public int SlotIndex { get; set; }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var player = GameManager.Instance?.CurrentPlayer as MadRealm.Player.Player;
        var item = player?.Inventory.GetSlot(SlotIndex);
        if (item == null)
            return default;

        var preview = new Label { Text = Text };
        SetDragPreview(preview);

        return SlotIndex;
    }
}
