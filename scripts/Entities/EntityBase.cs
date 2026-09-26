using Godot;
using MadRealm.Core;

namespace MadRealm.Entities;

// Named distinctly from the "Team" property below on purpose: a bare
// `Team.Player` reference inside a class that also has an instance
// member called `Team` resolves to that member first in C#, not the
// type, and fails to compile. Keeping the enum and property names
// different sidesteps that entirely.
public enum Faction
{
    Player,
    Enemy
}

// Shared base for anything that has HP and can die -- Player and every
// enemy type. Keeping health/damage/death logic here (instead of
// duplicated per-class) is what lets a bullet just call TakeDamage()
// on whatever it hit without caring if that's the player or a specific
// enemy type, and is the natural seam for an authoritative server to
// later own this state instead of the client.
public partial class EntityBase : CharacterBody2D
{
    [Export] public StatsResource Stats;
    [Export] public Faction Team = Faction.Enemy;

    public int CurrentHealth { get; protected set; }
    public bool IsDead { get; private set; }

    public override void _Ready()
    {
        // Absolute, not relative-to-parent -- guarantees Player/enemies
        // sit above the floor (-100) regardless of where in the tree
        // they live, same reasoning as FloorBackground.
        ZIndex = 0;
        ZAsRelative = false;

        if (Stats == null)
        {
            GD.PushWarning($"{Name} has no Stats resource assigned; using fallback defaults.");
            Stats = new StatsResource();
        }

        CurrentHealth = Stats.MaxHP;
    }

    public virtual void TakeDamage(int rawAmount, Node source)
    {
        if (IsDead)
            return;

        int finalDamage = Stats.ApplyDefense(rawAmount);
        CurrentHealth = System.Math.Max(0, CurrentHealth - finalDamage);

        EventBus.Instance.EmitSignal(EventBus.SignalName.DamageDealt, this, finalDamage, GlobalPosition);
        OnHealthChanged();

        if (CurrentHealth <= 0)
            Die();
    }

    // Overridden by Player to push PlayerHealthChanged; enemies don't
    // need a UI hook so the base no-ops.
    protected virtual void OnHealthChanged()
    {
    }

    public virtual void Die()
    {
        if (IsDead)
            return;

        IsDead = true;
        EventBus.Instance.EmitSignal(EventBus.SignalName.EntityDied, this);
        QueueFree();
    }
}
