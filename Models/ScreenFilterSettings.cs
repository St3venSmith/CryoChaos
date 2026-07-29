namespace CryoChaos.Models;

public readonly record struct ScreenFilterSettings(
    float Exposure,
    float Contrast,
    float Saturation,
    float HueDegrees,
    float Temperature,
    float Tint,
    float Gamma,
    float Red,
    float Green,
    float Blue,
    float Vignette)
{
    public static ScreenFilterSettings Default => new(
        Exposure: 0,
        Contrast: 1,
        Saturation: 1,
        HueDegrees: 0,
        Temperature: 0,
        Tint: 0,
        Gamma: 1,
        Red: 1,
        Green: 1,
        Blue: 1,
        Vignette: 0);
}
