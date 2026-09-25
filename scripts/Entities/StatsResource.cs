using Godot;

namespace MadRealm.Entities;

// The 8 RotMG-style stats. Living as a Resource (not hardcoded fields on
// Player/Enemy) means new classes, gear bonuses, or enemy tiers are
// authored as .tres data files in the editor -- no code changes, no
// recompiling, and designers/future-you can balance numbers without
// touching scripts.
[GlobalClass]
public partial class StatsResource : Resource
{
    [Export] public int MaxHP = 100;
    [Export] public int MaxMP = 100;
    [Export] public int Attack = 10;
    [Export] public int Defense = 0;
    [Export] public int Speed = 10;
    [Export] public int Dexterity = 10;
    [Export] public int Vitality = 10;
    [Export] public int Wisdom = 10;

    // Movement speed in px/sec. Tuned so Speed=10 (a starting class's
    // typical base) feels like a normal walk; scales up from there.
    public float MovementSpeed => 80f + Speed * 4f;

    // Seconds between shots. Higher Dexterity = faster firing, with a
    // floor so no amount of stacking makes fire rate silly.
    public float ShotCooldown => Mathf.Max(0.08f, 0.6f - Dexterity * 0.004f);

    // RotMG applies flat defense reduction per hit, with a minimum
    // chip-damage floor so stacking Defense can't make you unkillable.
    public int ApplyDefense(int incomingDamage)
    {
        int reduced = incomingDamage - Defense;
        int floor = System.Math.Max(1, incomingDamage / 10);
        return System.Math.Max(floor, reduced);
    }
}
