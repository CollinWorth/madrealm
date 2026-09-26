using Godot;
using MadRealm.Entities;
using MadRealm.Items;

namespace MadRealm.Core;

// Autoload singleton holding whole-run state that more than one system
// needs (current player reference, pause state, run seed). Deliberately
// thin for now -- this is the seam where "current dungeon/instance"
// tracking and eventual server-session state will hang once networking
// is added, so it stays a single known place rather than scattered
// static fields.
public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    public Node2D CurrentPlayer { get; set; }

    // Set by Portal right before a scene change, consumed by the next
    // scene's Player in _Ready(). ChangeSceneToFile() destroys the
    // entire current scene tree -- Player included -- so this is the
    // only thing that survives a portal trip; anything not explicitly
    // carried over here resets to that scene's defaults.
    public StatsResource PendingStats { get; set; }
    public int PendingHealth { get; set; } = -1;
    public Inventory PendingInventory { get; set; }
    public Equipment PendingEquipment { get; set; }

    public override void _Ready()
    {
        Instance = this;
    }
}
