using Godot;

namespace MadRealm.Core;

// Autoload singleton. Every cross-system notification (UI, audio, loot,
// future networking hooks) goes through here instead of nodes holding
// direct references to each other, so systems can be added or swapped
// without touching Player/Enemy code.
public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
    }

    [Signal]
    public delegate void PlayerHealthChangedEventHandler(int current, int max);

    [Signal]
    public delegate void PlayerManaChangedEventHandler(int current, int max);

    [Signal]
    public delegate void EntityDiedEventHandler(Node entity);

    [Signal]
    public delegate void PlayerDiedEventHandler();

    [Signal]
    public delegate void DamageDealtEventHandler(Node target, int amount, Vector2 atPosition);

    [Signal]
    public delegate void InventoryChangedEventHandler();
}
