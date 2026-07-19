namespace CryoChaos.Models;

public sealed class AppSettings
{
    public ChaosLevel SelectedLevel { get; set; } = ChaosLevel.Normal;
    public int MinimumIntervalSeconds { get; set; } = 35;
    public int MaximumIntervalSeconds { get; set; } = 70;
    public bool RequireDestinyForeground { get; set; } = true;
    public HashSet<string> DisabledEffectIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
