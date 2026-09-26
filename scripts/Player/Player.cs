using Godot;
using MadRealm.Core;
using MadRealm.Entities;
using MadRealm.Items;
using MadRealm.Projectiles;

namespace MadRealm.Player;

public partial class Player : EntityBase
{
    [Export] public PlayerClassData ClassData;
    [Export] public PackedScene BulletScene;

    public Inventory Inventory { get; set; } = new Inventory();

    private const uint LayerWorld = 1 << 0;
    private const uint LayerPlayer = 1 << 1;

    // Character sheet cell size and which column to use (column 5 =
    // "new style" blue character). Sheet layout: rows 0/1 = facing
    // down, 2/3 = facing sideways (drawn facing left; mirrored via
    // FlipH for right), 4/5 = facing away/up, each pair being a
    // 2-frame walk cycle -- only the first frame of each pair (0/2/4)
    // is used for now, see docs/art-integration-plan.md for the
    // walk-animation follow-up.
    private const int SpriteCellWidth = 16;
    private const int SpriteCellHeight = 24;
    private const int SpriteColumn = 5;

    // Index i corresponds to Inventory slot i -- Key1 uses slot 0, ...,
    // Key8 uses slot 7. Written out explicitly rather than computed
    // from Key.Key1 + i: Godot's Key enum values aren't guaranteed
    // contiguous just because the names look sequential, and this is
    // the kind of assumption that's cheap to avoid entirely.
    private static readonly Key[] HotkeySlots =
    {
        Key.Key1, Key.Key2, Key.Key3, Key.Key4,
        Key.Key5, Key.Key6, Key.Key7, Key.Key8,
    };

    private Sprite2D _sprite;
    private AtlasTexture _spriteAtlas;
    private float _aimAngle;
    private float _shotTimer;

    public override void _Ready()
    {
        Team = Faction.Player;

        // Arriving via a portal: adopt the stats/inventory Portal saved
        // on GameManager instead of building fresh ones from ClassData.
        // This is already this player's own duplicated Stats instance
        // from before the trip, so no further Duplicate() needed here.
        bool arrivedViaPortal = GameManager.Instance?.PendingStats != null;
        if (arrivedViaPortal)
        {
            Stats = GameManager.Instance.PendingStats;
            Inventory = GameManager.Instance.PendingInventory ?? Inventory;
        }
        // Duplicate, don't reference, the class's base stats -- this
        // Resource is about to become this specific player's live,
        // mutable HP/stat state. Without Duplicate(), every player
        // using the same class would share (and stomp) one Resource
        // instance, since Godot Resources are reference types by
        // default.
        else if (ClassData?.BaseStats != null)
        {
            Stats = (StatsResource)ClassData.BaseStats.Duplicate();
        }

        base._Ready();

        if (arrivedViaPortal && GameManager.Instance.PendingHealth >= 0)
            CurrentHealth = GameManager.Instance.PendingHealth;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PendingStats = null;
            GameManager.Instance.PendingHealth = -1;
            GameManager.Instance.PendingInventory = null;
        }

        CollisionLayer = LayerPlayer;
        CollisionMask = LayerWorld;

        _sprite = GetNode<Sprite2D>("CharacterSprite");
        _spriteAtlas = (AtlasTexture)_sprite.Texture;

        GameManager.Instance.CurrentPlayer = this;
        OnHealthChanged();
        QueueRedraw();
    }

    // The character sheet is drawn for 4 discrete facing directions,
    // not continuous rotation -- unlike the old placeholder circle,
    // it would look wrong spinning to track the mouse at arbitrary
    // angles. So CharacterSprite (a child of this node) is never
    // rotated; only this thin aim-line is, via _aimAngle directly
    // rather than the node's own Rotation (which stays 0 so the
    // sprite child inherits no rotation from its parent).
    public override void _Draw()
    {
        Vector2 aimDir = Vector2.Right.Rotated(_aimAngle);
        DrawLine(aimDir * 20f, aimDir * 46f, Colors.White, 4f);
    }

    public override void _PhysicsProcess(double delta)
    {
        HandleMovement();
        HandleAiming();
        HandleShooting((float)delta);
    }

    // Edge-triggered (Pressed && !Echo), not polled in _PhysicsProcess
    // like movement -- a potion should fire once per keypress, not
    // once per physics frame the key happens to still be held.
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return;

        for (int i = 0; i < HotkeySlots.Length; i++)
        {
            if (keyEvent.Keycode == HotkeySlots[i])
            {
                UseItemAt(i);
                return;
            }
        }
    }

    // Public: InventoryUI could also trigger this (e.g. a future
    // "right-click to use" interaction) without duplicating the
    // effect-application logic.
    public void UseItemAt(int slotIndex)
    {
        var item = Inventory.GetSlot(slotIndex);
        if (item == null || item.Effect == ItemEffectType.None)
            return;

        ApplyItemEffect(item);
        EventBus.Instance.EmitSignal(EventBus.SignalName.ItemUsed, item.ItemName, slotIndex);

        if (item.ConsumedOnUse)
        {
            Inventory.RemoveAt(slotIndex);
            EventBus.Instance.EmitSignal(EventBus.SignalName.InventoryChanged);
        }
    }

    private void ApplyItemEffect(ItemResource item)
    {
        switch (item.Effect)
        {
            case ItemEffectType.Heal:
                CurrentHealth = System.Math.Min(Stats.MaxHP, CurrentHealth + item.EffectAmount);
                OnHealthChanged();
                return;
            case ItemEffectType.BoostMaxHP:
                Stats.MaxHP += item.EffectAmount;
                CurrentHealth += item.EffectAmount; // grant the new capacity immediately, not just on next heal
                OnHealthChanged();
                break;
            case ItemEffectType.BoostMaxMP:
                Stats.MaxMP += item.EffectAmount;
                break;
            case ItemEffectType.BoostAttack:
                Stats.Attack += item.EffectAmount;
                break;
            case ItemEffectType.BoostDefense:
                Stats.Defense += item.EffectAmount;
                break;
            case ItemEffectType.BoostSpeed:
                Stats.Speed += item.EffectAmount;
                break;
            case ItemEffectType.BoostDexterity:
                Stats.Dexterity += item.EffectAmount;
                break;
            case ItemEffectType.BoostVitality:
                Stats.Vitality += item.EffectAmount;
                break;
            case ItemEffectType.BoostWisdom:
                Stats.Wisdom += item.EffectAmount;
                break;
            default:
                return;
        }

        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerStatsChanged);
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
        if (toMouse.LengthSquared() <= 1f)
            return;

        _aimAngle = toMouse.Angle();
        UpdateFacing(toMouse);
        QueueRedraw();
    }

    // Picks one of the sheet's 3 direction rows (down/side/up) by
    // whichever axis dominates the aim vector, mirroring the single
    // side-facing pose for right vs. left instead of needing a
    // separate right-facing frame that doesn't exist on the sheet.
    private void UpdateFacing(Vector2 direction)
    {
        if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y))
        {
            _spriteAtlas.Region = new Rect2(SpriteColumn * SpriteCellWidth, 2 * SpriteCellHeight, SpriteCellWidth, SpriteCellHeight);
            _sprite.FlipH = direction.X > 0f;
        }
        else if (direction.Y > 0f)
        {
            _spriteAtlas.Region = new Rect2(SpriteColumn * SpriteCellWidth, 0 * SpriteCellHeight, SpriteCellWidth, SpriteCellHeight);
            _sprite.FlipH = false;
        }
        else
        {
            _spriteAtlas.Region = new Rect2(SpriteColumn * SpriteCellWidth, 4 * SpriteCellHeight, SpriteCellWidth, SpriteCellHeight);
            _sprite.FlipH = false;
        }
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
        Vector2 direction = Vector2.Right.Rotated(_aimAngle);
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
