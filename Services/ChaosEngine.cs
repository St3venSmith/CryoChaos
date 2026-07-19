using System.Collections.ObjectModel;
using System.Diagnostics;
using CryoChaos.Effects;
using CryoChaos.Models;
using CryoChaos.Views;

namespace CryoChaos.Services;

public sealed class ChaosEngine : IDisposable
{
    private readonly IReadOnlyList<IChaosEffect> _effects;
    private readonly Dictionary<string, DateTimeOffset> _cooldowns =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IChaosEffect> _activeEffects =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _sync = new();
    private readonly Random _random = new();
    private readonly OverlayWindow _overlay;
    private readonly DestinyKeybindService _keybinds;
    private readonly KeyboardInputService _input;
    private readonly IScreenTransformService _screenTransform;

    private CancellationTokenSource? _runCancellation;
    private Task? _loopTask;

    public ChaosEngine(
        OverlayWindow overlay,
        DestinyKeybindService keybinds,
        KeyboardInputService input,
        IScreenTransformService screenTransform)
    {
        _overlay = overlay;
        _keybinds = keybinds;
        _input = input;
        _screenTransform = screenTransform;

        _effects =
        [
            new RedTintEffect(),
            new TunnelVisionEffect(),
            new BlackoutPulseEffect(),
            new MovingBlockEffect(),

            new UpsideDownEffect(),
            new SidewaysLeftEffect(),
            new SidewaysRightEffect(),
            new MirrorScreenEffect(),
            new VerticalFlipEffect(),

            new SurpriseJumpEffect(),
            new ForcedCrouchEffect(),
            new WeaponPanicEffect(),
            new StepBackwardEffect()
        ];

        Effects = new ObservableCollection<ChaosEffectDefinition>(
            _effects.Select(effect => effect.Definition));
    }

    public ObservableCollection<ChaosEffectDefinition> Effects { get; }

    public bool IsRunning => _runCancellation is not null;

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

        lock (_sync)
        {
            _activeEffects.Clear();
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
                remaining < TimeSpan.FromMilliseconds(100)
                    ? remaining
                    : TimeSpan.FromMilliseconds(100);

            await Task.Delay(
                updateDelay,
                cancellationToken);
        }
    }

    private IChaosEffect? SelectEffect(
        ChaosLevel selectedLevel)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        List<IChaosEffect> eligible;

        lock (_sync)
        {
            bool anyActiveEffect =
                _activeEffects.Count > 0;

            bool nonStackableEffectIsActive =
                _activeEffects.Values.Any(effect =>
                    !effect.Definition.CanStack);

            eligible = _effects
                .Where(effect =>
                    effect.Definition.Enabled &&
                    effect.Definition.MinimumLevel <= selectedLevel &&
                    (!_cooldowns.TryGetValue(
                         effect.Definition.Id,
                         out DateTimeOffset readyAt) ||
                     readyAt <= now) &&
                    !_activeEffects.ContainsKey(
                        effect.Definition.Id) &&
                    !nonStackableEffectIsActive &&
                    (effect.Definition.CanStack ||
                     !anyActiveEffect))
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
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            // A non-stackable effect may have started after this effect was
            // selected. Recheck before starting it.
            bool anyActiveEffect =
                _activeEffects.Count > 0;

            bool nonStackableEffectIsActive =
                _activeEffects.Values.Any(activeEffect =>
                    !activeEffect.Definition.CanStack);

            if (nonStackableEffectIsActive ||
                (!effect.Definition.CanStack &&
                 anyActiveEffect))
            {
                return;
            }

            _activeEffects[effect.Definition.Id] = effect;

            _cooldowns[effect.Definition.Id] =
                DateTimeOffset.UtcNow.AddSeconds(
                    effect.Definition.CooldownSeconds);
        }

        TimeSpan displayDuration =
            TimeSpan.FromSeconds(
                Math.Max(
                    1,
                    effect.Definition.DurationSeconds));

        _overlay.AddActiveEffect(
            effect.Definition.Id,
            effect.Definition.Name,
            displayDuration);

        EffectStarted?.Invoke(
            this,
            effect.Definition.Name);

        Stopwatch displayStopwatch =
            Stopwatch.StartNew();

        try
        {
            ChaosEffectContext context = new()
            {
                SelectedLevel = selectedLevel,
                Overlay = _overlay,
                Keybinds = _keybinds,
                Input = _input,
                ScreenTransform = _screenTransform,
                Random = _random,
                RequireDestinyForeground =
                    requireDestinyForeground
            };

            await effect.RunAsync(
                context,
                cancellationToken);

            // One-shot keybind effects finish almost immediately. Keep their
            // name visible for the configured duration so the player can read it.
            TimeSpan remainingDisplay =
                displayDuration - displayStopwatch.Elapsed;

            if (remainingDisplay > TimeSpan.Zero)
            {
                await Task.Delay(
                    remainingDisplay,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
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
                _activeEffects.Remove(
                    effect.Definition.Id);
            }

            EffectFinished?.Invoke(
                this,
                effect.Definition.Name);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
