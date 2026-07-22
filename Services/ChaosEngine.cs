using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using CryoChaos.Effects;
using CryoChaos.Models;
using CryoChaos.Views;

namespace CryoChaos.Services;

public sealed class ChaosEngine : IDisposable
{
    public const int MaximumSupportedActiveEffects = 50;
    private const int StandardEffectWeight = 10;
    private const int ScreenTransformWeight = 1;
    private const int RareMutatorWeight = 1;
    private int _maximumActiveEffects = 3;

    private readonly IReadOnlyList<IChaosEffect> _effects;
    private readonly Dictionary<string, DateTimeOffset> _cooldowns =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, ActiveEffect> _activeEffects =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _sync = new();
    private readonly Random _random = new();
    private readonly OverlayWindow _overlay;
    private readonly DestinyKeybindService _keybinds;
    private readonly KeyboardInputService _input;
    private readonly KeyboardRemapService _inputRemapper;
    private readonly RawMouseEffectService _rawMouseEffects;
    private readonly SoundEffectService _soundEffects;
    private readonly GameAudioEffectService _gameAudioEffects;
    private readonly VideoOverlayService _videoOverlay;
    private readonly QteService _qte;
    private readonly IScreenTransformService _screenTransform;
    private readonly ChaosMutatorService _mutators;

    private CancellationTokenSource? _runCancellation;
    private Task? _loopTask;

    public ChaosEngine(
        OverlayWindow overlay,
        DestinyKeybindService keybinds,
        KeyboardInputService input,
        KeyboardRemapService inputRemapper,
        RawMouseEffectService rawMouseEffects,
        SoundEffectService soundEffects,
        GameAudioEffectService gameAudioEffects,
        VideoOverlayService videoOverlay,
        QteService qte,
        IScreenTransformService screenTransform)
    {
        _overlay = overlay;
        _keybinds = keybinds;
        _input = input;
        _inputRemapper = inputRemapper;
        _rawMouseEffects = rawMouseEffects;
        _soundEffects = soundEffects;
        _gameAudioEffects = gameAudioEffects;
        _videoOverlay = videoOverlay;
        _qte = qte;
        _screenTransform = screenTransform;
        _mutators = new ChaosMutatorService(CancelOtherEffects);

        _effects = DiscoverEffects();

        // Keep ordinary effects evenly distributed even as new classes are
        // auto-discovered. Screen transforms are deliberately much rarer as
        // a group because there are many more transform variants than most
        // other effect families.
        foreach (IChaosEffect effect in _effects)
        {
            effect.Definition.Weight = effect.Definition.Id is
                "mutator_purge_effects" or "mutator_protection"
                    ? RareMutatorWeight
                    : effect.Definition.Type == ChaosEffectType.ScreenTransform
                        ? ScreenTransformWeight
                        : StandardEffectWeight;
        }

        Effects = new ObservableCollection<ChaosEffectDefinition>(
            _effects.Select(effect => effect.Definition));
    }

    private static IReadOnlyList<IChaosEffect> DiscoverEffects()
    {
        Type effectInterface = typeof(IChaosEffect);

        List<IChaosEffect> effects = effectInterface.Assembly
            .GetTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                effectInterface.IsAssignableFrom(type) &&
                type.GetCustomAttribute<ChaosEffectTemplateAttribute>() is null)
            .Select(CreateEffect)
            .OrderBy(effect => effect.Definition.Type)
            .ThenBy(effect => effect.Definition.MinimumLevel)
            .ThenBy(effect => effect.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string[] duplicateIds = effects
            .GroupBy(
                effect => effect.Definition.Id,
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Chaos effect IDs must be unique. Duplicates: {string.Join(", ", duplicateIds)}");
        }

        return effects;
    }

    private static IChaosEffect CreateEffect(Type effectType)
    {
        try
        {
            return (IChaosEffect)(Activator.CreateInstance(effectType) ??
                throw new InvalidOperationException("Activator returned null."));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not create chaos effect '{effectType.FullName}'. " +
                "Effects must have a parameterless constructor.",
                ex);
        }
    }

    public ObservableCollection<ChaosEffectDefinition> Effects { get; }

    public bool IsRunning => _runCancellation is not null;

    public void SetMaximumActiveEffects(int value)
    {
        if (value is < 1 or > MaximumSupportedActiveEffects)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"Maximum active effects must be between 1 and {MaximumSupportedActiveEffects}.");
        }

        lock (_sync)
        {
            _maximumActiveEffects = value;
        }
    }

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? EffectStarted;
    public event EventHandler<string>? EffectFinished;

    public void Start(
        ChaosLevel selectedLevel,
        int minimumIntervalSeconds,
        int maximumIntervalSeconds,
        bool requireDestinyForeground)
    {
        Stop();

        if (minimumIntervalSeconds < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumIntervalSeconds));
        }

        if (maximumIntervalSeconds < minimumIntervalSeconds)
        {
            throw new ArgumentException(
                "Maximum interval must be at least the minimum interval.");
        }

        _runCancellation = new CancellationTokenSource();
        _overlay.SetEngineRunning(true);

        _loopTask = RunLoopAsync(
            selectedLevel,
            minimumIntervalSeconds,
            maximumIntervalSeconds,
            requireDestinyForeground,
            _runCancellation.Token);

        StatusChanged?.Invoke(
            this,
            $"Running: {selectedLevel}");
    }

    public void Stop()
    {
        CancellationTokenSource? cancellation = _runCancellation;
        _runCancellation = null;

        if (cancellation is not null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }

        ActiveEffect[] active;
        lock (_sync)
        {
            active = _activeEffects.Values.ToArray();
            _activeEffects.Clear();
        }

        foreach (ActiveEffect effect in active)
        {
            TryCancel(effect.Cancellation);
        }

        _ = _screenTransform.StopAsync();

        _overlay.ClearAllEffects();
        _overlay.SetEngineRunning(false);
        StatusChanged?.Invoke(this, "Stopped");
    }

    public async Task TriggerRandomNowAsync(
        ChaosLevel selectedLevel,
        bool requireDestinyForeground,
        CancellationToken cancellationToken = default)
    {
        IChaosEffect? selected = SelectEffect(selectedLevel);

        if (selected is null)
        {
            StatusChanged?.Invoke(
                this,
                "No eligible effect is currently available.");
            return;
        }

        await StartEffectAsync(
            selected,
            selectedLevel,
            requireDestinyForeground,
            cancellationToken);
    }

    private async Task RunLoopAsync(
        ChaosLevel selectedLevel,
        int minimumIntervalSeconds,
        int maximumIntervalSeconds,
        bool requireDestinyForeground,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int delaySeconds = _random.Next(
                    minimumIntervalSeconds,
                    maximumIntervalSeconds + 1);

                TimeSpan delay =
                    TimeSpan.FromSeconds(delaySeconds);

                StatusChanged?.Invoke(
                    this,
                    $"Next effect in {delaySeconds} seconds");

                await RunCountdownAsync(
                    delay,
                    cancellationToken);

                IChaosEffect? selected =
                    SelectEffect(selectedLevel);

                if (selected is null)
                {
                    StatusChanged?.Invoke(
                        this,
                        "No eligible effect available; rerolling timer.");
                    continue;
                }

                _ = StartEffectAsync(
                    selected,
                    selectedLevel,
                    requireDestinyForeground,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            CrashLogService.WriteException(
                "CHAOS ENGINE LOOP",
                ex);
            StatusChanged?.Invoke(
                this,
                $"Engine error: {ex.Message}");
        }
    }

    private async Task RunCountdownAsync(
        TimeSpan total,
        CancellationToken cancellationToken)
    {
        DateTimeOffset endsAt =
            DateTimeOffset.UtcNow.Add(total);

        _overlay.UpdateNextEffectCountdown(
            total,
            total);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TimeSpan remaining =
                endsAt - DateTimeOffset.UtcNow;

            if (remaining <= TimeSpan.Zero)
            {
                _overlay.UpdateNextEffectCountdown(
                    TimeSpan.Zero,
                    total);
                return;
            }

            _overlay.UpdateNextEffectCountdown(
                remaining,
                total);

            TimeSpan updateDelay =
                remaining < TimeSpan.FromMilliseconds(33)
                    ? remaining
                    : TimeSpan.FromMilliseconds(33);

            await Task.Delay(
                updateDelay,
                cancellationToken);
        }
    }

    private IChaosEffect? SelectEffect(
        ChaosLevel selectedLevel,
        bool requireStackable = false,
        bool excludeMutators = false)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        List<IChaosEffect> eligible;

        lock (_sync)
        {
            if (_activeEffects.Count >= _maximumActiveEffects)
            {
                return null;
            }

            eligible = _effects
                .Where(effect =>
                    effect.Definition.Enabled &&
                    (!excludeMutators || effect.Definition.Type != ChaosEffectType.Mutator) &&
                    (!requireStackable || effect.Definition.CanStack) &&
                    effect.Definition.MinimumLevel <= selectedLevel &&
                    (!_cooldowns.TryGetValue(
                         effect.Definition.Id,
                         out DateTimeOffset readyAt) ||
                     readyAt <= now) &&
                    !_activeEffects.ContainsKey(
                        effect.Definition.Id) &&
                    (effect.Definition.Type == ChaosEffectType.Mutator ||
                     (!_mutators.IsProtected && CanStartOrdinaryEffect(
                         effect,
                         _activeEffects.Values))))
                .ToList();
        }

        if (eligible.Count == 0)
        {
            return null;
        }

        int totalWeight = eligible.Sum(effect =>
            Math.Max(0, effect.Definition.Weight));

        if (totalWeight <= 0)
        {
            return eligible[_random.Next(eligible.Count)];
        }

        int roll = _random.Next(totalWeight);

        foreach (IChaosEffect effect in eligible)
        {
            roll -= Math.Max(
                0,
                effect.Definition.Weight);

            if (roll < 0)
            {
                return effect;
            }
        }

        return eligible[^1];
    }

    private async Task StartEffectAsync(
        IChaosEffect effect,
        ChaosLevel selectedLevel,
        bool requireDestinyForeground,
        CancellationToken cancellationToken,
        bool suppressMutatorDuplication = false)
    {
        int queuedRandomEffects = 0;
        CancellationTokenSource effectCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ActiveEffect activeEffect = new(effect, effectCancellation);

        lock (_sync)
        {
            // A non-stackable effect may have started after this effect was
            // selected. Recheck before starting it.
            bool isMutator =
                effect.Definition.Type == ChaosEffectType.Mutator;

            if (_activeEffects.Count >= _maximumActiveEffects ||
                (!isMutator && _mutators.IsProtected) ||
                (!isMutator && !CanStartOrdinaryEffect(
                    effect,
                    _activeEffects.Values)))
            {
                effectCancellation.Dispose();
                return;
            }

            _activeEffects[effect.Definition.Id] = activeEffect;

            _cooldowns[effect.Definition.Id] =
                DateTimeOffset.UtcNow.AddSeconds(
                    effect.Definition.CooldownSeconds);
        }

        double runtimeDurationMultiplier =
            _mutators.DurationMultiplier;

        TimeSpan displayDuration =
            TimeSpan.FromSeconds(
                Math.Max(
                    1,
                    effect.Definition.DurationSeconds)) *
            ChaosEffectContext.GetDurationMultiplier(selectedLevel) *
            runtimeDurationMultiplier;

        _overlay.AddActiveEffect(
            effect.Definition.Id,
            effect.Definition.Name,
            displayDuration);

        EffectStarted?.Invoke(
            this,
            effect.Definition.Name);

        Stopwatch displayStopwatch =
            Stopwatch.StartNew();

        if (!suppressMutatorDuplication &&
            effect.Definition.Type != ChaosEffectType.Mutator)
        {
            int extraEffects = _mutators.ExtraEffectCount;
            for (int index = 0; index < extraEffects; index++)
            {
                IChaosEffect? extra = SelectEffect(
                    selectedLevel,
                    requireStackable: true,
                    excludeMutators: true);
                if (extra is null)
                {
                    break;
                }

                _ = StartEffectAsync(
                    extra,
                    selectedLevel,
                    requireDestinyForeground,
                    cancellationToken,
                    suppressMutatorDuplication: true);
            }
        }

        try
        {
            ChaosEffectContext context = new()
            {
                SelectedLevel = selectedLevel,
                Overlay = _overlay,
                Keybinds = _keybinds,
                Input = _input,
                InputRemapper = _inputRemapper,
                RawMouseEffects = _rawMouseEffects,
                SoundEffects = _soundEffects,
                GameAudioEffects = _gameAudioEffects,
                VideoOverlay = _videoOverlay,
                Qte = _qte,
                Mutators = _mutators,
                CurrentEffectId = effect.Definition.Id,
                RuntimeDurationMultiplier = runtimeDurationMultiplier,
                QueueRandomEffects = count =>
                    Interlocked.Add(
                        ref queuedRandomEffects,
                        Math.Clamp(count, 0, 10)),
                TryTriggerRandomEffectNow = () =>
                    TryStartRandomEffectNow(
                        selectedLevel,
                        requireDestinyForeground,
                        cancellationToken),
                ScreenTransform = _screenTransform,
                Random = _random,
                RequireDestinyForeground =
                    requireDestinyForeground
            };

            await effect.RunAsync(
                context,
                effectCancellation.Token);

            // One-shot keybind effects finish almost immediately. Keep their
            // name visible for the configured duration so the player can read it.
            TimeSpan remainingDisplay =
                displayDuration - displayStopwatch.Elapsed;

            // A substituted/penalty effect should begin as soon as the
            // current effect requests it, rather than waiting out the
            // current effect's display-only duration.
            if (queuedRandomEffects == 0 &&
                remainingDisplay > TimeSpan.Zero)
            {
                await Task.Delay(
                    remainingDisplay,
                    effectCancellation.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            CrashLogService.WriteException(
                $"EFFECT FAILED: {effect.Definition.Id}",
                ex);
            StatusChanged?.Invoke(
                this,
                $"{effect.Definition.Name} failed: {ex.Message}");
        }
        finally
        {
            _overlay.RemoveActiveEffect(
                effect.Definition.Id);

            lock (_sync)
            {
                if (_activeEffects.TryGetValue(
                        effect.Definition.Id,
                        out ActiveEffect? current) &&
                    ReferenceEquals(current, activeEffect))
                {
                    _activeEffects.Remove(effect.Definition.Id);
                }
            }

            effectCancellation.Dispose();

            EffectFinished?.Invoke(
                this,
                effect.Definition.Name);
        }

        if (queuedRandomEffects > 0 &&
            !cancellationToken.IsCancellationRequested)
        {
            for (int index = 0; index < queuedRandomEffects; index++)
            {
                IChaosEffect? penalty = SelectEffect(selectedLevel, requireStackable: true);
                if (penalty is null)
                {
                    break;
                }

                _ = StartEffectAsync(
                    penalty,
                    selectedLevel,
                    requireDestinyForeground,
                    cancellationToken);
            }
        }
    }

    private static bool CanStartOrdinaryEffect(
        IChaosEffect candidate,
        IEnumerable<ActiveEffect> activeEffects)
    {
        ActiveEffect[] active = activeEffects.ToArray();
        bool candidateIsMouse = candidate is RawMouseChaosEffectBase;

        // Raw-mouse modifiers share one interception pipeline. Only one mouse
        // modifier may run at a time, but it must not prevent an unrelated
        // keyboard, audio, graphic, or screen effect from running beside it.
        if (candidateIsMouse)
        {
            return active.All(item =>
                item.Effect is not RawMouseChaosEffectBase);
        }

        ActiveEffect[] relevant = active
            .Where(item =>
                item.Effect.Definition.Type != ChaosEffectType.Mutator &&
                item.Effect is not RawMouseChaosEffectBase)
            .ToArray();

        return relevant.All(item => item.Effect.Definition.CanStack) &&
               (candidate.Definition.CanStack || relevant.Length == 0);
    }

    public void Dispose()
    {
        Stop();
    }

    private bool TryStartRandomEffectNow(
        ChaosLevel selectedLevel,
        bool requireDestinyForeground,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        IChaosEffect? selected = SelectEffect(selectedLevel, requireStackable: true);
        if (selected is null)
        {
            return false;
        }

        _ = StartEffectAsync(
            selected,
            selectedLevel,
            requireDestinyForeground,
            cancellationToken);
        return true;
    }

    private void CancelOtherEffects(string currentEffectId)
    {
        ActiveEffect[] toCancel;
        lock (_sync)
        {
            toCancel = _activeEffects
                .Where(pair => !pair.Key.Equals(
                    currentEffectId,
                    StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Value)
                .ToArray();
        }

        foreach (ActiveEffect effect in toCancel)
        {
            TryCancel(effect.Cancellation);
        }
    }

    private static void TryCancel(CancellationTokenSource cancellation)
    {
        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The effect completed between the active-state snapshot and the
            // cancellation pass.
        }
    }

    private sealed record ActiveEffect(
        IChaosEffect Effect,
        CancellationTokenSource Cancellation);

}
