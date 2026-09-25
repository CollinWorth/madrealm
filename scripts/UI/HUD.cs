using Godot;
using MadRealm.Core;

namespace MadRealm.UI;

// Listens to EventBus instead of holding a Player reference -- the HUD
// doesn't need to know Player exists at all, just that "some health
// changed" events happen. Swapping HUD layouts or adding a second
// display (minimap, party frames) later doesn't touch Player code.
public partial class HUD : CanvasLayer
{
    private ProgressBar _healthBar;
    private Label _healthLabel;

    public override void _Ready()
    {
        _healthBar = GetNode<ProgressBar>("HealthBar");
        _healthLabel = GetNode<Label>("HealthBar/HealthLabel");

        EventBus.Instance.PlayerHealthChanged += OnPlayerHealthChanged;
    }

    private void OnPlayerHealthChanged(int current, int max)
    {
        _healthBar.MaxValue = max;
        _healthBar.Value = current;
        _healthLabel.Text = $"{current} / {max}";
    }
}
