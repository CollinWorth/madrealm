using Godot;

namespace MadRealm.World;

// Placeholder floor rendering for the test arena -- no art assets yet,
// just enough to see the play space. Swap for a TileMap once there's
// real level geometry / procedural room generation.
public partial class Arena : Node2D
{
    [Export] public Vector2 ArenaSize = new Vector2(1200f, 800f);
    [Export] public Color FloorColor = new Color(0.08f, 0.08f, 0.1f);

    public override void _Ready()
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 half = ArenaSize / 2f;
        DrawRect(new Rect2(-half, ArenaSize), FloorColor);
    }
}
