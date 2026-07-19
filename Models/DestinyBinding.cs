namespace CryoChaos.Models;

public sealed class DestinyBinding
{
    public required string Action { get; init; }
    public string? PrimaryRaw { get; init; }
    public string? SecondaryRaw { get; init; }
    public InputBinding? Primary { get; init; }
    public InputBinding? Secondary { get; init; }

    public string PrimaryDisplay =>
        Primary?.ToString() ?? PrimaryRaw ?? "Unused";

    public string SecondaryDisplay =>
        Secondary?.ToString() ?? SecondaryRaw ?? "Unused";
}
