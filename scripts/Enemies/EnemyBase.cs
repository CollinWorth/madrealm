using Godot;
using MadRealm.Core;
using MadRealm.Entities;
using MadRealm.Projectiles;

namespace MadRealm.Enemies;

// Idle -> chase -> attack, driven entirely by distance to the player
// and a BulletPatternResource. Every enemy in the game uses this same
// script; variety comes from swapping the exported Stats/Pattern data
// in the editor, not from subclassing. Only reach for a real subclass
// (or a proper state machine node) once an enemy needs behavior this
// simple distance check genuinely can't express.
public partial class EnemyBase : EntityBase
{
    [Export] public PackedScene BulletScene;
    [Export] public BulletPatternResource Pattern;
    [Export] public float AggroRange = 300f;
    [Export] public float AttackRange = 220f;
    [Export] public float FireCooldown = 1.5f;

    private const uint LayerWorld = 1 << 0;
    private const uint LayerEnemy = 1 << 2;

    private float _fireTimer;

    public override void _Ready()
    {
        Team = Faction.Enemy;
        base._Ready();

        CollisionLayer = LayerEnemy;
        CollisionMask = LayerWorld;

        _fireTimer = FireCooldown;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 13f, Colors.Black);
        DrawCircle(Vector2.Zero, 10f, new Color(0.8f, 0.15f, 0.2f));
    }

    public override void _PhysicsProcess(double delta)
    {
        var player = GameManager.Instance?.CurrentPlayer;
        if (player == null || !GodotObject.IsInstanceValid(player))
        {
            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }

        float distance = GlobalPosition.DistanceTo(player.GlobalPosition);

        if (distance <= AggroRange)
        {
            Velocity = distance > AttackRange
                ? (player.GlobalPosition - GlobalPosition).Normalized() * Stats.MovementSpeed * 0.5f
                : Vector2.Zero;

            HandleFiring(player.GlobalPosition, (float)delta);
        }
        else
        {
            Velocity = Vector2.Zero;
        }

        MoveAndSlide();
    }

    private void HandleFiring(Vector2 targetPosition, float delta)
    {
        if (Pattern == null || BulletScene == null)
            return;

        _fireTimer -= delta;
        if (_fireTimer > 0f)
            return;

        FirePattern(targetPosition);
        _fireTimer = FireCooldown;
    }

    private void FirePattern(Vector2 targetPosition)
    {
        Vector2 baseDirection = Pattern.AimAtPlayer
            ? (targetPosition - GlobalPosition).Normalized()
            : Vector2.Right.Rotated(Rotation);

        float baseAngle = baseDirection.Angle();
        float spreadRad = Mathf.DegToRad(Pattern.SpreadDegrees);
        int count = System.Math.Max(1, Pattern.BulletCount);

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle;
            if (count > 1)
            {
                // Center each bullet within its own equal slice of the
                // total spread (rather than spacing by count - 1,
                // which puts a bullet on each extreme edge). That
                // alternative breaks for a full 360-degree ring
                // specifically: the first and last bullets would both
                // land exactly on the seam, firing two bullets in the
                // same direction instead of evenly spacing all of them.
                float t = (i + 0.5f) / count;
                angle = baseAngle - spreadRad / 2f + spreadRad * t;
            }

            var bullet = ObjectPool.Instance.Get<Bullet>(BulletScene);
            Vector2 direction = Vector2.Right.Rotated(angle);
            bullet.Fire(GlobalPosition, direction, Pattern.BulletSpeed, Stats.Attack, Faction.Enemy, Pattern.BulletColor, Pattern.BulletLifetime);
        }
    }
}
