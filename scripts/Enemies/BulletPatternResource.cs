using Godot;
using MadRealm.Core;
using MadRealm.Entities;
using MadRealm.Projectiles;

namespace MadRealm.Enemies;

// A single attack an enemy can perform, as data. RotMG-style enemies
// are defined almost entirely by their bullet patterns -- this is the
// piece that lets adding a new enemy "type" mean authoring a new
// .tres (spread shot, ring burst, aimed sniper shot, ...) instead of
// writing a new script per enemy.
//
// Owns its own firing behavior for the same reason ItemResource owns
// a weapon's -- EnemyBase.HandleFiring only ever ticks the cooldown
// and asks ReadyToFire()/Fire(); it never spawns bullets or tracks a
// timer itself. Two patterns (ring burst vs. aimed sniper shot) now
// behave completely differently purely through data.
[GlobalClass]
public partial class BulletPatternResource : Resource
{
    [Export] public PackedScene BulletScene;
    [Export] public int BulletCount = 1;
    [Export] public float SpreadDegrees = 0f;
    [Export] public float BulletSpeed = 250f;
    [Export] public float BulletLifetime = 3f;
    [Export] public bool AimAtPlayer = true;
    [Export] public Color BulletColor = Colors.Red;

    // Seconds between shots. Unlike a weapon's, this isn't further
    // scaled by any stat -- enemies don't have a Dexterity-equivalent
    // fire-rate stat, so the pattern's own number is the whole story.
    [Export] public float FireCooldown = 1.5f;

    private float _cooldownRemaining;

    public bool ReadyToFire() => _cooldownRemaining <= 0f;

    public void TickCooldown(float delta)
    {
        if (_cooldownRemaining > 0f)
            _cooldownRemaining -= delta;
    }

    // Fire() itself enforces ReadyToFire() and does the AimAtPlayer/
    // facing-direction decision that used to live in EnemyBase.FirePattern
    // -- same reasoning as ItemResource.Fire: the pattern owns every
    // property that shapes the shot, so it's the one place that logic
    // needs to live. Returns whether it actually fired.
    public bool Fire(Vector2 origin, Vector2 targetPosition, float facingRotation, int damage, Faction team)
    {
        if (!ReadyToFire() || BulletScene == null)
            return false;

        Vector2 baseDirection = AimAtPlayer
            ? (targetPosition - origin).Normalized()
            : Vector2.Right.Rotated(facingRotation);

        float baseAngle = baseDirection.Angle();
        float spreadRad = Mathf.DegToRad(SpreadDegrees);
        int count = System.Math.Max(1, BulletCount);

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
            bullet.Fire(origin, direction, BulletSpeed, damage, team, BulletColor, BulletLifetime);
        }

        _cooldownRemaining = FireCooldown;
        return true;
    }
}
