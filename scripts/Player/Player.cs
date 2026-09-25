using Godot;
using MadRealm.Core;
using MadRealm.Entities;
using MadRealm.Projectiles;

namespace MadRealm.Player;

public partial class Player : EntityBase
{
    [Export] public PlayerClassData ClassData;
    [Export] public PackedScene BulletScene;

    private const uint LayerWorld = 1 << 0;
    private const uint LayerPlayer = 1 << 1;

    private float _shotTimer;

    public override void _Ready()
    {
        Team = Faction.Player;

        // Duplicate, don't reference, the class's base stats -- this
        // Resource is about to become this specific player's live,
        // mutable HP/stat state. Without Duplicate(), every player
        // using the same class would share (and stomp) one Resource
        // instance, since Godot Resources are reference types by
        // default.
        if (ClassData?.BaseStats != null)
            Stats = (StatsResource)ClassData.BaseStats.Duplicate();

        base._Ready();

        CollisionLayer = LayerPlayer;
        CollisionMask = LayerWorld;

        GameManager.Instance.CurrentPlayer = this;
        OnHealthChanged();
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 14f, Colors.White);
        DrawCircle(Vector2.Zero, 11f, new Color(0.2f, 0.6f, 1f));
        // Facing indicator, drawn along local +X since Rotation is applied by the engine.
        DrawLine(Vector2.Zero, new Vector2(16f, 0f), Colors.White, 3f);
    }

    public override void _PhysicsProcess(double delta)
    {
        HandleMovement();
        HandleAiming();
        HandleShooting((float)delta);
    }

    private void HandleMovement()
    {
        Vector2 input = Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.W)) input.Y -= 1f;
        if (Input.IsPhysicalKeyPressed(Key.S)) input.Y += 1f;
        if (Input.IsPhysicalKeyPressed(Key.A)) input.X -= 1f;
        if (Input.IsPhysicalKeyPressed(Key.D)) input.X += 1f;

        Velocity = input.Normalized() * Stats.MovementSpeed;
        MoveAndSlide();
    }

    private void HandleAiming()
    {
        Vector2 toMouse = GetGlobalMousePosition() - GlobalPosition;
        if (toMouse.LengthSquared() > 1f)
            Rotation = toMouse.Angle();
    }

    private void HandleShooting(float delta)
    {
        _shotTimer -= delta;
        if (_shotTimer > 0f)
            return;

        if (Input.IsMouseButtonPressed(MouseButton.Left))
        {
            Fire();
            _shotTimer = Stats.ShotCooldown;
        }
    }

    private void Fire()
    {
        if (BulletScene == null)
            return;

        var bullet = ObjectPool.Instance.Get<Bullet>(BulletScene);
        Vector2 direction = Vector2.Right.Rotated(Rotation);
        Color color = ClassData?.ProjectileColor ?? Colors.Cyan;
        float speed = ClassData?.ProjectileSpeed ?? 600f;
        float lifetime = ClassData?.ProjectileLifetime ?? 1.2f;

        bullet.Fire(GlobalPosition, direction, speed, Stats.Attack, Faction.Player, color, lifetime);
    }

    protected override void OnHealthChanged()
    {
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, CurrentHealth, Stats.MaxHP);
    }

    public override void Die()
    {
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerDied);
        base.Die();
    }
}
