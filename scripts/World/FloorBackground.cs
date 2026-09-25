using Godot;

namespace MadRealm.World;

// Deliberately its own node, not drawn by the room root directly.
// CanvasItem z-index is relative-to-parent by default, so putting a
// low z-index on the room root itself would drag every child (Player,
// enemies, walls) down with it. Worse, pooled bullets live under the
// ObjectPool autoload -- a different branch of the tree entirely from
// the room, added to the scene root before the room even loads -- so
// sibling draw order can't make bullets render above the floor no
// matter how the room's own children are ordered. ZAsRelative = false
// pins this at an absolute z-index regardless of nesting, which is
// the only thing that reliably wins across separate tree branches.
public partial class FloorBackground : Node2D
{
    [Export] public Vector2 ArenaSize = new Vector2(1200f, 800f);
    [Export] public Color FloorColor = new Color(0.08f, 0.08f, 0.1f);

    public override void _Ready()
    {
        ZIndex = -100;
        ZAsRelative = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 half = ArenaSize / 2f;
        DrawRect(new Rect2(-half, ArenaSize), FloorColor);
    }
}
