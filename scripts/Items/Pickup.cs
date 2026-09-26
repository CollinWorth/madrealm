using Godot;
using MadRealm.Core;

namespace MadRealm.Items;

// World-placed item. Walking a Player into it adds Item to their
// Inventory and removes itself. If the inventory is full it just sits
// there -- no special "full" feedback yet, that's a UI polish item for
// later, not a functional gap.
public partial class Pickup : Area2D
{
    [Export] public ItemResource Item;

    private const uint LayerPlayer = 1 << 1;

    public override void _Ready()
    {
        // Absolute z-index, same layer as entities -- see the comment
        // on EntityBase._Ready() / FloorBackground for why this needs
        // to be explicit rather than left at the relative default.
        ZIndex = 0;
        ZAsRelative = false;

        CollisionLayer = 0;
        CollisionMask = LayerPlayer;
        BodyEntered += OnBodyEntered;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Color color = Item?.IconColor ?? Colors.White;
        DrawCircle(Vector2.Zero, 8f, color);
        DrawCircle(Vector2.Zero, 8f, Colors.Black, false, 2f);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (Item == null)
            return;

        if (body is not MadRealm.Player.Player player)
            return;

        if (!player.Inventory.TryAdd(Item))
            return;

        EventBus.Instance.EmitSignal(EventBus.SignalName.InventoryChanged);
        QueueFree();
    }
}
