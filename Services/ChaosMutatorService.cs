namespace CryoChaos.Services;

/// <summary>
/// Holds temporary modifiers that alter effects started by ChaosEngine. Each
/// modifier is reference-counted so independently running mutators clean up
/// only their own state when cancelled.
/// </summary>
public sealed class ChaosMutatorService
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, double> _durationMultipliers = [];
    private readonly HashSet<Guid> _duplicateModifiers = [];
    private readonly HashSet<Guid> _protectionModifiers = [];
    private readonly Action<string> _cancelOtherEffects;

    public ChaosMutatorService(Action<string> cancelOtherEffects) =>
        _cancelOtherEffects = cancelOtherEffects;

    public double DurationMultiplier
    {
        get
        {
            lock (_sync)
            {
                return Math.Clamp(
                    _durationMultipliers.Values.Aggregate(1.0, (total, value) => total * value),
                    1.0,
                    6.0);
            }
        }
    }

    public int ExtraEffectCount
    {
        get
        {
            lock (_sync)
            {
                // The original activation plus one immediate extra activation
                // produces a two-effect stack.
                return _duplicateModifiers.Count > 0 ? 1 : 0;
            }
        }
    }

    public bool IsProtected
    {
        get
        {
            lock (_sync)
            {
                return _protectionModifiers.Count > 0;
            }
        }
    }

    public Task DoubleDurationAsync(TimeSpan duration, CancellationToken token) =>
        ApplyDurationAsync(2.0, duration, token);

    public Task TripleDurationAsync(TimeSpan duration, CancellationToken token) =>
        ApplyDurationAsync(3.0, duration, token);

    public Task DuplicateNewEffectsAsync(TimeSpan duration, CancellationToken token) =>
        ApplyFlagAsync(_duplicateModifiers, duration, token);

    public Task ProtectAsync(TimeSpan duration, CancellationToken token) =>
        ApplyFlagAsync(_protectionModifiers, duration, token);

    public void RemoveAllOtherEffects(string currentEffectId) =>
        _cancelOtherEffects(currentEffectId);

    private async Task ApplyDurationAsync(
        double multiplier,
        TimeSpan duration,
        CancellationToken token)
    {
        Guid id = Guid.NewGuid();
        lock (_sync)
        {
            _durationMultipliers[id] = multiplier;
        }

        try
        {
            await Task.Delay(duration, token);
        }
        finally
        {
            lock (_sync)
            {
                _durationMultipliers.Remove(id);
            }
        }
    }

    private async Task ApplyFlagAsync(
        HashSet<Guid> flags,
        TimeSpan duration,
        CancellationToken token)
    {
        Guid id = Guid.NewGuid();
        lock (_sync)
        {
            flags.Add(id);
        }

        try
        {
            await Task.Delay(duration, token);
        }
        finally
        {
            lock (_sync)
            {
                flags.Remove(id);
            }
        }
    }
}
