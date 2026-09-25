using Godot;
using MadRealm.Entities;

namespace MadRealm.Player;

// A playable class (Wizard, Archer, Knight, ...) as data, not a
// subclass. Adding a new class later is authoring a new .tres resource
// that points at a base StatsResource and a projectile look, not
// writing a new script -- same reasoning as StatsResource.
[GlobalClass]
public partial class PlayerClassData : Resource
{
    [Export] public string ClassName = "Wizard";
    [Export] public StatsResource BaseStats;
    [Export] public Color ProjectileColor = Colors.Cyan;
    [Export] public float ProjectileSpeed = 600f;
    [Export] public float ProjectileLifetime = 1.2f;
}
