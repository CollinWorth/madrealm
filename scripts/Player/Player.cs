using Godot;
using MadRealm.Core;
using MadRealm.Entities;
using MadRealm.Items;

namespace MadRealm.Player;

public partial class Player : EntityBase
{
	[Export] public PlayerClassData ClassData;

	public Inventory Inventory { get; set; } = new Inventory();
	public Equipment Equipment { get; set; } = new Equipment();

	// Tuned by feel, not a real formula -- see docs/architecture.md if
	// this needs to match a specific design target later. Vitality=10
	// (a typical starting value) heals ~2 HP/sec; the Wizard's 15
	// heals ~3 HP/sec.
	private const float RegenPerVitalityPerSecond = 0.2f;
	private float _regenAccumulator;

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
			Equipment = GameManager.Instance.PendingEquipment ?? Equipment;
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
			GameManager.Instance.PendingEquipment = null;
		}

		// Fresh spawn with nothing equipped yet: the class's innate
		// weapon goes straight into the Weapon slot, not the inventory
		// -- a class that can attack should never start unable to fire.
		// Duplicated for the same reason equip-from-inventory duplicates
		// (see EquipFromInventory): this is about to become live,
		// per-player cooldown-timer state.
		if (!arrivedViaPortal && Equipment.Weapon == null && ClassData?.StartingWeapon != null)
		{
			Equipment.SetSlot(ItemType.Weapon, (ItemResource)ClassData.StartingWeapon.Duplicate());
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
		HandleRegen((float)delta);
	}

	// Accumulates fractional regen (CurrentHealth is an int, Vitality-
	// scaled regen per frame usually isn't a whole number) and only
	// applies -- and only fires OnHealthChanged -- once a whole point
	// is actually banked, rather than every single frame.
	private void HandleRegen(float delta)
	{
		if (CurrentHealth >= Stats.MaxHP)
		{
			_regenAccumulator = 0f;
			return;
		}

		_regenAccumulator += Stats.Vitality * RegenPerVitalityPerSecond * delta;
		if (_regenAccumulator < 1f)
			return;

		int wholePoints = (int)_regenAccumulator;
		_regenAccumulator -= wholePoints;
		CurrentHealth = System.Math.Min(Stats.MaxHP, CurrentHealth + wholePoints);
		OnHealthChanged();
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

	// Equips the item sitting in the given Inventory slot, if it's
	// gear (Weapon/Armor/Ring). Whatever was previously equipped in
	// that slot (if anything) goes back into the exact inventory slot
	// index the new item just vacated -- net-neutral on slot count,
	// so this can never fail due to a full inventory the way a
	// generic TryAdd() could. Consumables aren't equippable here;
	// they only ever go through UseItemAt.
	public void EquipFromInventory(int inventorySlotIndex)
	{
		var newItem = Inventory.GetSlot(inventorySlotIndex);
		if (newItem == null)
			return;

		if (newItem.Kind != ItemType.Weapon && newItem.Kind != ItemType.Armor && newItem.Kind != ItemType.Ring)
			return;

		// Duplicate on equip, not just for weapons specifically -- gear
		// .tres files are shared/cached by Godot's resource loader
		// (every pickup pointing at the same .tres path is the *same*
		// object in memory), and equipping is the moment an item starts
		// carrying live, per-player state (a weapon's fire cooldown
		// today; any future armor/ring mechanic with its own runtime
		// state gets this for free too). Skipping this would mean two
		// different pickups of "the same" item could end up silently
		// sharing one cooldown clock.
		var equippedItem = (ItemResource)newItem.Duplicate();

		var previousItem = Equipment.GetSlot(equippedItem.Kind);
		if (previousItem != null)
			RemoveGearBonus(previousItem);

		Equipment.SetSlot(equippedItem.Kind, equippedItem);
		ApplyGearBonus(equippedItem);

		Inventory.SetSlot(inventorySlotIndex, previousItem);

		EventBus.Instance.EmitSignal(EventBus.SignalName.InventoryChanged);
		EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerStatsChanged);
	}

	// Adds every Bonus* field on the item straight onto the live
	// Stats resource. Safe to call repeatedly with different items --
	// each equip/unequip pair is a precise add then subtract of the
	// exact same numbers, so stacking never drifts.
	private void ApplyGearBonus(ItemResource item)
	{
		Stats.MaxHP += item.BonusMaxHP;
		Stats.MaxMP += item.BonusMaxMP;
		Stats.Attack += item.BonusAttack;
		Stats.Defense += item.BonusDefense;
		Stats.Speed += item.BonusSpeed;
		Stats.Dexterity += item.BonusDexterity;
		Stats.Vitality += item.BonusVitality;
		Stats.Wisdom += item.BonusWisdom;
		CurrentHealth = System.Math.Min(CurrentHealth, Stats.MaxHP);
		OnHealthChanged();
	}

	// Mirror of ApplyGearBonus. The CurrentHealth clamp matters here
	// specifically: unequipping a +MaxHP item can drop MaxHP below
	// whatever CurrentHealth currently is.
	private void RemoveGearBonus(ItemResource item)
	{
		Stats.MaxHP -= item.BonusMaxHP;
		Stats.MaxMP -= item.BonusMaxMP;
		Stats.Attack -= item.BonusAttack;
		Stats.Defense -= item.BonusDefense;
		Stats.Speed -= item.BonusSpeed;
		Stats.Dexterity -= item.BonusDexterity;
		Stats.Vitality -= item.BonusVitality;
		Stats.Wisdom -= item.BonusWisdom;
		CurrentHealth = System.Math.Min(CurrentHealth, Stats.MaxHP);
		OnHealthChanged();
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

	// Player no longer spawns bullets or tracks a fire-rate timer
	// itself -- both live on the equipped weapon (ItemResource.Fire()/
	// ReadyToFire()). This is just: tick the weapon's cooldown, and if
	// the trigger's held and the weapon says it's ready, tell it to
	// fire. Two different weapons (spread vs. single shot, fast vs.
	// slow) now behave completely differently with zero changes here.
	private void HandleShooting(float delta)
	{
		var weapon = Equipment.Weapon;
		if (weapon == null)
			return;

		weapon.TickCooldown(delta);

		if (Input.IsMouseButtonPressed(MouseButton.Left) && weapon.ReadyToFire())
		{
			weapon.Fire(GlobalPosition, _aimAngle, Stats.Attack, Faction.Player, Stats);
		}
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
