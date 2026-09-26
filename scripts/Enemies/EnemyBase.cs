using Godot;
using MadRealm.Core;
using MadRealm.Entities;

namespace MadRealm.Enemies;

// Idle -> chase -> attack, driven entirely by distance to the player
// and a BulletPatternResource. Every enemy in the game uses this same
// script; variety comes from swapping the exported Stats/Pattern data
// in the editor, not from subclassing. Only reach for a real subclass
// (or a proper state machine node) once an enemy needs behavior this
// simple distance check genuinely can't express.
public partial class EnemyBase : EntityBase
{
	[Export] public BulletPatternResource Pattern;
	[Export] public float AggroRange = 300f;
	[Export] public float AttackRange = 220f;

	private const uint LayerWorld = 1 << 0;
	private const uint LayerEnemy = 1 << 2;

	public override void _Ready()
	{
		Team = Faction.Enemy;
		base._Ready();

		CollisionLayer = LayerEnemy;
		CollisionMask = LayerWorld;

		// Same reasoning as Player.EquipFromInventory duplicating gear:
		// Godot caches/shares Resources loaded from the same .tres path,
		// and multiple enemies in a room routinely point at the same
		// pattern (e.g. two Swarmers both using SpreadBurst.tres).
		// Pattern now carries live per-shooter cooldown state, so
		// without duplicating it here those enemies would share one
		// fire-rate clock and silently starve each other.
		if (Pattern != null)
			Pattern = (BulletPatternResource)Pattern.Duplicate();

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

	// EnemyBase no longer tracks a fire-rate timer or spawns bullets
	// itself -- both live on Pattern (BulletPatternResource.Fire()/
	// ReadyToFire()), same split as Player/ItemResource. This is just:
	// tick the pattern's cooldown, and if it's ready, tell it to fire.
	private void HandleFiring(Vector2 targetPosition, float delta)
	{
		if (Pattern == null)
			return;

		Pattern.TickCooldown(delta);

		if (Pattern.ReadyToFire())
			Pattern.Fire(GlobalPosition, targetPosition, Rotation, Stats.Attack, Faction.Enemy);
	}
}
