using Godot;

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

    public override void _Ready()
    {
        Instance = this;
    }
}
