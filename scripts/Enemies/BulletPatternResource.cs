using Godot;

namespace MadRealm.Enemies;

// A single attack an enemy can perform, as data. RotMG-style enemies
// are defined almost entirely by their bullet patterns -- this is the
// piece that lets adding a new enemy "type" mean authoring a new
// .tres (spread shot, ring burst, aimed sniper shot, ...) instead of
// writing a new script per enemy.
[GlobalClass]
public partial class BulletPatternResource : Resource
{
    [Export] public int BulletCount = 1;
    [Export] public float SpreadDegrees = 0f;
    [Export] public float BulletSpeed = 250f;
    [Export] public float BulletLifetime = 3f;
    [Export] public bool AimAtPlayer = true;
    [Export] public Color BulletColor = Colors.Red;
}
