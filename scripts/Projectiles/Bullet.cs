using Godot;
using MadRealm.Core;
using MadRealm.Entities;

namespace MadRealm.Projectiles;

// Pooled projectile. Spawned via ObjectPool.Get<Bullet>() and returned
// via Despawn() -> ObjectPool.Release() rather than QueueFree(), so a
// screen full of bullets (the whole point of this genre) doesn't mean
// constant node allocation. Layer/mask assignment below matches the
// [layer_names] table in project.godot -- keep them in sync.
public partial class Bullet : Area2D
{
    private Vector2 _direction = Vector2.Right;
    private float _speed = 400f;
    private int _damage = 10;
    private Faction _team = Faction.Enemy;
    private Color _color = Colors.White;

    private Timer _lifetimeTimer;

    private const uint LayerWorld = 1 << 0;
    private const uint LayerPlayer = 1 << 1;
    private const uint LayerEnemy = 1 << 2;
    private const uint LayerPlayerBullet = 1 << 3;
    private const uint LayerEnemyBullet = 1 << 4;

    public override void _Ready()
    {
        // Absolute z-index, deliberately above entities (0) and the
        // floor (-100): bullets are pooled under the ObjectPool
        // autoload, a different tree branch than the room/entities
        // entirely, and are the thing you most need to see clearly to
        // dodge, so they get explicit top priority rather than
        // whatever tree-order happened to fall out of that split.
        ZIndex = 5;
        ZAsRelative = false;

        _lifetimeTimer = GetNode<Timer>("Lifetime");
        _lifetimeTimer.Timeout += Despawn;
        BodyEntered += OnBodyEntered;
    }

    // Called by whatever fired this (PlayerShooter, EnemyBase) right
    // after pulling it from the pool.
    public void Fire(Vector2 origin, Vector2 direction, float speed, int damage, Faction team, Color color, float lifetime = 2.5f)
    {
        GlobalPosition = origin;
        _direction = direction.Normalized();
        _speed = speed;
        _damage = damage;
        _team = team;
        _color = color;
        Rotation = _direction.Angle();

        if (_team == Faction.Player)
        {
            CollisionLayer = LayerPlayerBullet;
            CollisionMask = LayerEnemy;
        }
        else
        {
            CollisionLayer = LayerEnemyBullet;
            CollisionMask = LayerPlayer;
        }

        _lifetimeTimer.Start(lifetime);
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        GlobalPosition += _direction * _speed * (float)delta;
    }

    public override void _Draw()
    {
        // All bullets render white regardless of team/pattern color for
        // now (easier to see against everything while testing). _color
        // is still tracked per-shot, so reverting to team/pattern
        // colors later is just swapping Colors.White back for _color
        // here -- nothing upstream needs to change.
        DrawCircle(Vector2.Zero, 4f, Colors.White);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is EntityBase entity && entity.Team != _team)
        {
            entity.TakeDamage(_damage, this);
            Despawn();
        }
    }

    private void Despawn()
    {
        _lifetimeTimer.Stop();
        ObjectPool.Instance.Release(this);
    }
}
