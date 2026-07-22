namespace CryoChaos.Models;

public enum ScreenTransformMode
{
    // These explicit values are sent directly to EffectMode in
    // Shaders/ScreenEffects.hlsl. Never renumber existing modes.
    None = 0,
    Rotate90Clockwise = 1,
    Rotate90CounterClockwise = 2,
    Rotate180 = 3,
    FlipHorizontal = 4,
    FlipVertical = 5,
    Grayscale = 6,
    InvertColors = 7,
    Pixelate = 8,
    ChromaticAberration = 9,
    Wave = 10,
    Kaleidoscope = 11,
    Sepia = 12,
    Posterize = 13,
    Scanlines = 14,
    VignettePulse = 15,
    ZoomPulse = 16,
    DigitalGlitch = 17,
    LensWarp = 18,
    HeatVision = 19,
    ColorCycle = 20,
    ScreenShake = 21,
    MirrorTiles = 22,
    DreamBlur = 23,
    NightVision = 24
}
