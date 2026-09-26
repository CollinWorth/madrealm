using Godot;
using MadRealm.Entities;
using MadRealm.Items;

namespace MadRealm.Player;

// A playable class (Wizard, Archer, Knight, ...) as data, not a
// subclass. Adding a new class later is authoring a new .tres resource
// that points at a base StatsResource and a starting weapon, not
// writing a new script -- same reasoning as StatsResource.
//
// Used to also carry ProjectileColor/Speed/Lifetime directly -- those
// moved onto ItemResource (see its Weapon* fields) once weapons became
// responsible for their own firing behavior instead of Player reading
// class data for it. A class's "look" now comes from whatever weapon
// it starts with, not from the class itself.
[GlobalClass]
public partial class PlayerClassData : Resource
{
    [Export] public string ClassName = "Wizard";
    [Export] public StatsResource BaseStats;

    // Auto-equipped on first spawn (not on portal arrival -- see
    // Player._Ready()) if nothing's already equipped. A weapon slot is
    // never meant to be empty for a class that can attack at all;
    // this is the class's innate wand/bow/whatever, not optional gear.
    [Export] public ItemResource StartingWeapon;
}
