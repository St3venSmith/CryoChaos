# CryoChaos

A .NET 8 WPF starter project for a Destiny 2 chaos controller.

## Included

- Low, Normal, and Chaos tiers.
- Higher tiers inherit all lower-tier effects.
- Graphic effects rendered through a transparent, click-through overlay.
- Keybind effects that use the player's detected Destiny bindings.
- Automatic read-only monitoring of:

  `%APPDATA%\Bungie\DestinyPC\prefs\cvars.xml`

- Weighted random effects, cooldowns, enable/disable controls, settings persistence, and a foreground safety check.

## Build

1. Install Visual Studio 2022 with the **.NET desktop development** workload.
2. Open `CryoChaos.csproj`.
3. Build for `x64` or `Any CPU`.
4. Start Destiny 2 once so that `cvars.xml` exists.
5. Run CryoChaos, inspect the **Detected keybinds** tab, then press **Start**.

## Important limitations

Destiny's internal action names can differ between versions or control setups. The resolver first checks exact aliases, then fuzzy-matches action names. Review the detected-bindings tab before using keybind effects.

This project does not inject into Destiny, read game memory, alter game files, evade anti-cheat, aim, fire, farm, or react to game state. It uses normal desktop overlays and Windows `SendInput` only.

## Fixed-size in-game HUD

The overlay now includes a fixed 720-DIP progress bar at the top center of the primary display and a fixed 330-DIP current-effect card below its right side. WPF device-independent pixels keep this HUD a consistent physical size across different game resolutions and Windows DPI settings.

The progress bar drains toward zero until the next random effect. The current-effect card shows the newest active effect, its remaining time, and a count when multiple effects overlap.

Blackout pulses now reach and hold 100% opacity. Tunnel Vision and Screen Intruder use fully opaque black areas.

## Live screen-transform effects

This build adds a separate full-screen live-view window powered by the native
Windows Magnification API. It copies the visible Destiny 2 client area and can
rotate or flip it while the normal CryoChaos HUD remains above it.

Included effects:

- Upside Down — 180-degree rotation
- Sideways Left — 90-degree counterclockwise rotation
- Sideways Right — 90-degree clockwise rotation
- Mirror Mode — horizontal flip
- Vertical Flip — top-to-bottom flip

The project is forced to x64 because Microsoft does not support the
Magnification API from a 32-bit process running under WOW64.

For best results, run Destiny 2 in Borderless Windowed mode. Exclusive
fullscreen applications may not be available to the desktop compositor and can
produce a black live view. CryoChaos excludes its own top-level windows from the
magnifier so the progress bar and app window are not recursively captured.

The main files are:

- `Models/ScreenTransformMode.cs`
- `Services/MagnificationNative.cs`
- `Services/DestinyWindowService.cs`
- `Services/ScreenTransformService.cs`
- `Views/MagnifierHost.cs`
- `Views/ScreenTransformWindow.xaml`
- `Views/ScreenTransformWindow.xaml.cs`
- `Effects/ScreenTransformEffects.cs`

No additional NuGet packages are required.
