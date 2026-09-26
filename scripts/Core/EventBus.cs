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

    [Signal]
    public delegate void ItemUsedEventHandler(string itemName, int slotIndex);

    // Fired after any permanent StatsResource change (item boosts,
    // eventually equipment/leveling). Deliberately parameterless --
    // listeners just re-read whatever they need from
    // GameManager.CurrentPlayer.Stats rather than this signal trying
    // to describe every possible stat change shape up front.
    [Signal]
    public delegate void PlayerStatsChangedEventHandler();
}
