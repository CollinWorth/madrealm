using Godot;

namespace MadRealm.World;

// Gives StaticBody2D walls/pillars an actual visual instead of
// invisible collision-only geometry. Set exactly one of RectSize /
// CircleRadius to match whatever CollisionShape2D is on the same
// node -- drawn size should always match what you actually bump into.
public partial class Obstacle : StaticBody2D
{
    [Export] public Vector2 RectSize = Vector2.Zero;
    [Export] public float CircleRadius = 0f;
    [Export] public Color ObstacleColor = new Color(0.25f, 0.24f, 0.28f);

    public override void _Ready()
    {
        ZIndex = 0;
        ZAsRelative = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (RectSize != Vector2.Zero)
            DrawRect(new Rect2(-RectSize / 2f, RectSize), ObstacleColor);
        else if (CircleRadius > 0f)
            DrawCircle(Vector2.Zero, CircleRadius, ObstacleColor);
    }
}
