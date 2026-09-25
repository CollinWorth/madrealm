using Godot;
using MadRealm.Core;
using MadRealm.Items;

namespace MadRealm.UI;

// Toggled with I. Reads/writes Player.Inventory directly through
// GameManager rather than tracking its own copy of the data, so it
// can never go stale relative to what's actually in the inventory.
// Clicking a filled slot drops that item back into the world at the
// player's position and clears the slot -- simplest possible
// interaction that exercises the full pickup -> inventory -> drop
// loop without needing an equip/stat-bonus system yet.
public partial class InventoryUI : CanvasLayer
{
    [Export] public PackedScene PickupScene;

    private Control _panel;
    private Button[] _slots;

    public override void _Ready()
    {
        _panel = GetNode<Control>("Panel");
        _panel.Visible = false;

        _slots = new Button[Inventory.Size];
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i] = GetNode<Button>($"Panel/Grid/Slot{i}");

            // Capture a per-iteration copy -- `i` itself is reused
            // across loop iterations in a `for` loop (unlike foreach),
            // so every closure would otherwise see the same final
            // value of `i` once the loop finishes.
            int index = i;
            _slots[i].Pressed += () => OnSlotPressed(index);
        }

        EventBus.Instance.InventoryChanged += RefreshSlots;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.I)
        {
            _panel.Visible = !_panel.Visible;
            if (_panel.Visible)
                RefreshSlots();
        }
    }

    private void RefreshSlots()
    {
        var inventory = GetPlayerInventory();
        if (inventory == null)
            return;

        for (int i = 0; i < _slots.Length; i++)
        {
            var item = inventory.GetSlot(i);
            if (item == null)
            {
                _slots[i].Text = "";
                _slots[i].TooltipText = "Empty";
                _slots[i].Modulate = Colors.White;
            }
            else
            {
                _slots[i].Text = item.ItemName.Length > 3 ? item.ItemName.Substring(0, 3) : item.ItemName;
                _slots[i].TooltipText = $"{item.ItemName} ({item.Kind})\n{item.Description}\nClick to drop";
                _slots[i].Modulate = item.IconColor;
            }
        }
    }

    private void OnSlotPressed(int index)
    {
        var player = GameManager.Instance?.CurrentPlayer as MadRealm.Player.Player;
        var inventory = player?.Inventory;
        if (inventory == null)
            return;

        var item = inventory.RemoveAt(index);
        if (item == null)
            return;

        if (PickupScene != null)
        {
            var pickup = (Pickup)PickupScene.Instantiate();
            pickup.Item = item;
            pickup.GlobalPosition = player.GlobalPosition;
            player.GetParent().AddChild(pickup);
        }

        EventBus.Instance.EmitSignal(EventBus.SignalName.InventoryChanged);
    }

    private Inventory GetPlayerInventory()
    {
        var player = GameManager.Instance?.CurrentPlayer as MadRealm.Player.Player;
        return player?.Inventory;
    }
}
