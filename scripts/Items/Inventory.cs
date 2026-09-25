namespace MadRealm.Items;

// Plain C# class, not a Node or Resource -- it's per-player runtime
// state with no reason to live in the scene tree. Player owns one
// instance directly. Callers (Pickup, InventoryUI) are responsible for
// firing EventBus.InventoryChanged after a change; Inventory itself
// stays a dumb data holder rather than depending on Godot's signal
// system, so it's trivial to unit-test in isolation later if it ever
// needs to be.
public class Inventory
{
    public const int Size = 8;

    private readonly ItemResource[] _slots = new ItemResource[Size];

    public ItemResource GetSlot(int index) => _slots[index];

    public bool TryAdd(ItemResource item)
    {
        for (int i = 0; i < Size; i++)
        {
            if (_slots[i] == null)
            {
                _slots[i] = item;
                return true;
            }
        }

        return false;
    }

    public ItemResource RemoveAt(int index)
    {
        if (index < 0 || index >= Size)
            return null;

        var item = _slots[index];
        _slots[index] = null;
        return item;
    }
}
