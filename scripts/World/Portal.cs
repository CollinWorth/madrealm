using Godot;
using MadRealm.Core;

namespace MadRealm.World;

// Walking into a portal saves the player's current run state (stats,
// health, inventory) onto GameManager, then loads the destination
// scene. That save step is required, not optional: ChangeSceneToFile
// throws away the entire current scene tree -- including the Player
// node itself -- so without carrying state over explicitly, every
// portal trip would silently reset your HP and empty your inventory.
public partial class Portal : Area2D
{
    [Export] public string DestinationScenePath;
    [Export] public Color PortalColor = new Color(0.6f, 0.2f, 0.9f);

    private const uint LayerPlayer = 1 << 1;

    public override void _Ready()
    {
        ZIndex = 0;
        ZAsRelative = false;
        CollisionLayer = 0;
        CollisionMask = LayerPlayer;
        BodyEntered += OnBodyEntered;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 24f, PortalColor);
        DrawCircle(Vector2.Zero, 24f, Colors.White, false, 3f);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (string.IsNullOrEmpty(DestinationScenePath))
            return;

        if (body is not MadRealm.Player.Player player)
            return;

        GameManager.Instance.PendingStats = player.Stats;
        GameManager.Instance.PendingHealth = player.CurrentHealth;
        GameManager.Instance.PendingInventory = player.Inventory;

        GetTree().ChangeSceneToFile(DestinationScenePath);
    }
}
