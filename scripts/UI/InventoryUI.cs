using Godot;
using MadRealm.Core;
using MadRealm.Items;

namespace MadRealm.UI;

// Toggled with I. The combined character-sheet-and-inventory panel:
// stats + health bar + 3 equipment slots on one side, the 8-slot
// inventory grid on the other, so dragging an item from inventory
// onto a gear slot (see InventorySlotButton/EquipmentSlotButton) has
// both ends visible at once. Reads/writes Player state directly
// through GameManager rather than tracking its own copy, so it can
// never go stale relative to what's actually equipped/held.
public partial class InventoryUI : CanvasLayer
{
    [Export] public PackedScene PickupScene;

    private Control _panel;
    private InventorySlotButton[] _slots;

    private ProgressBar _healthBar;
    private Label _healthLabel;
    private Label _attackLabel;
    private Label _defenseLabel;
    private Label _speedLabel;
    private Label _dexterityLabel;
    private Label _vitalityLabel;
    private Label _wisdomLabel;

    private EquipmentSlotButton _weaponSlot;
    private EquipmentSlotButton _armorSlot;
    private EquipmentSlotButton _ringSlot;

    public override void _Ready()
    {
        _panel = GetNode<Control>("Panel");
        _panel.Visible = false;

        _slots = new InventorySlotButton[Inventory.Size];
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i] = GetNode<InventorySlotButton>($"Panel/Grid/Slot{i}");
            _slots[i].SlotIndex = i;

            // Capture a per-iteration copy -- `i` itself is reused
            // across loop iterations in a `for` loop (unlike foreach),
            // so every closure would otherwise see the same final
            // value of `i` once the loop finishes.
            int index = i;
            _slots[i].Pressed += () => OnSlotPressed(index);
        }

        _healthBar = GetNode<ProgressBar>("Panel/HealthBar");
        _healthLabel = GetNode<Label>("Panel/HealthBar/HealthLabel");
        _attackLabel = GetNode<Label>("Panel/StatsBox/AttackLabel");
        _defenseLabel = GetNode<Label>("Panel/StatsBox/DefenseLabel");
        _speedLabel = GetNode<Label>("Panel/StatsBox/SpeedLabel");
        _dexterityLabel = GetNode<Label>("Panel/StatsBox/DexterityLabel");
        _vitalityLabel = GetNode<Label>("Panel/StatsBox/VitalityLabel");
        _wisdomLabel = GetNode<Label>("Panel/StatsBox/WisdomLabel");

        _weaponSlot = GetNode<EquipmentSlotButton>("Panel/GearBox/WeaponSlot");
        _armorSlot = GetNode<EquipmentSlotButton>("Panel/GearBox/ArmorSlot");
        _ringSlot = GetNode<EquipmentSlotButton>("Panel/GearBox/RingSlot");

        EventBus.Instance.InventoryChanged += RefreshSlots;
        EventBus.Instance.PlayerHealthChanged += OnPlayerHealthChanged;
        EventBus.Instance.PlayerStatsChanged += RefreshStats;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.I)
        {
            _panel.Visible = !_panel.Visible;
            if (_panel.Visible)
            {
                RefreshSlots();
                RefreshStats();
            }
        }
    }

    private void OnPlayerHealthChanged(int current, int max)
    {
        _healthBar.MaxValue = max;
        _healthBar.Value = current;
        _healthLabel.Text = $"{current} / {max}";
    }

    private void RefreshStats()
    {
        var player = GameManager.Instance?.CurrentPlayer as MadRealm.Player.Player;
        if (player == null)
            return;

        _attackLabel.Text = $"Attack: {player.Stats.Attack}";
        _defenseLabel.Text = $"Defense: {player.Stats.Defense}";
        _speedLabel.Text = $"Speed: {player.Stats.Speed}";
        _dexterityLabel.Text = $"Dexterity: {player.Stats.Dexterity}";
        _vitalityLabel.Text = $"Vitality: {player.Stats.Vitality}";
        _wisdomLabel.Text = $"Wisdom: {player.Stats.Wisdom}";

        OnPlayerHealthChanged(player.CurrentHealth, player.Stats.MaxHP);
        RefreshEquipmentSlots(player);
    }

    private void RefreshEquipmentSlots(MadRealm.Player.Player player)
    {
        SetEquipSlotDisplay(_weaponSlot, player.Equipment.Weapon);
        SetEquipSlotDisplay(_armorSlot, player.Equipment.Armor);
        SetEquipSlotDisplay(_ringSlot, player.Equipment.Ring);
    }

    private void SetEquipSlotDisplay(Button slotButton, ItemResource item)
    {
        if (item == null)
        {
            slotButton.Text = "";
            slotButton.TooltipText = "Empty";
            slotButton.Modulate = Colors.White;
        }
        else
        {
            slotButton.Text = item.ItemName.Length > 3 ? item.ItemName.Substring(0, 3) : item.ItemName;
            slotButton.TooltipText = $"{item.ItemName}\n{item.Description}";
            slotButton.Modulate = item.IconColor;
        }
    }

    private void RefreshSlots()
    {
        var inventory = GetPlayerInventory();
        if (inventory == null)
            return;

        for (int i = 0; i < _slots.Length; i++)
        {
            var item = inventory.GetSlot(i);
            if (item == null)
            {
                _slots[i].Text = "";
                _slots[i].TooltipText = "Empty";
                _slots[i].Modulate = Colors.White;
            }
            else
            {
                _slots[i].Text = item.ItemName.Length > 3 ? item.ItemName.Substring(0, 3) : item.ItemName;
                bool isGear = item.Kind == ItemType.Weapon || item.Kind == ItemType.Armor || item.Kind == ItemType.Ring;
                string hint = item.Effect != ItemEffectType.None
                    ? $"Press {i + 1} to use, click to drop"
                    : isGear
                        ? "Drag to a gear slot to equip, click to drop"
                        : "Click to drop";
                _slots[i].TooltipText = $"{item.ItemName} ({item.Kind})\n{item.Description}\n{hint}";
                _slots[i].Modulate = item.IconColor;
            }
        }

        // Equipping swaps an item back into Inventory too, so keep the
        // equipment slot display in sync from this same signal.
        var player = GameManager.Instance?.CurrentPlayer as MadRealm.Player.Player;
        if (player != null)
            RefreshEquipmentSlots(player);
    }

    private void OnSlotPressed(int index)
    {
        var player = GameManager.Instance?.CurrentPlayer as MadRealm.Player.Player;
        var inventory = player?.Inventory;
        if (inventory == null)
            return;

        var item = inventory.RemoveAt(index);
        if (item == null)
            return;

        if (PickupScene != null)
        {
            var pickup = (Pickup)PickupScene.Instantiate();
            pickup.Item = item;
            pickup.GlobalPosition = player.GlobalPosition;
            player.GetParent().AddChild(pickup);
        }

        EventBus.Instance.EmitSignal(EventBus.SignalName.InventoryChanged);
    }

    private Inventory GetPlayerInventory()
    {
        var player = GameManager.Instance?.CurrentPlayer as MadRealm.Player.Player;
        return player?.Inventory;
    }
}
