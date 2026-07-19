using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CryoChaos.Models;

public sealed class ChaosEffectDefinition : INotifyPropertyChanged
{
    private bool _enabled = true;

    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required ChaosEffectType Type { get; init; }
    public required ChaosLevel MinimumLevel { get; init; }
    public required int Weight { get; init; }
    public required int DurationSeconds { get; init; }
    public required int CooldownSeconds { get; init; }
    public bool CanStack { get; init; }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
