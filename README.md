# CryoChaos

## Live game-audio effects

CryoChaos can capture only the `destiny2.exe` process tree through Windows
WASAPI process loopback and layer a processed wet signal over a quieter dry
signal. Included effects are Echo, Reverse, Underwater, Pitch Up, Pitch Down,
and Reverb. This requires Windows 10 build 20348 or later.

The game is not injected or hooked. While an audio effect is active, CryoChaos
temporarily lowers Destiny's Windows mixer session to 22 percent, renders the
effected stream through the default output device, and restores the user's exact
previous mixer volume when the effect stops or fails. Keep Destiny and
CryoChaos on the same default Windows output device.

## Raw mouse physics

The external Raw Input correction service also supports Momentum, Elastic,
Gravity, Magnet, Orbit, a 20-count Deadzone, Wind, and accelerating Friction.
The effects use the same bounded 8 ms output pump and injection marker as the
camera lock and sensitivity effects. Press `Ctrl+Alt+F12` to stop a raw-mouse
effect immediately.

CryoChaos is an external Destiny 2 chaos controller. It applies timed visual and input effects without injecting code into the game process.

## GPU screen effects

Live screen effects use this low-latency path:

1. Windows Graphics Capture provides Direct3D 11 textures for the Destiny window.
2. A free-threaded two-frame capture pool requests a 1 ms minimum update interval.
3. The renderer samples the rotating WGC textures directly without a CPU readback or intermediate full-frame copy.
4. An HLSL shader transforms the image.
5. The same WPF/native preview window used by the successful capture
   diagnostic presents through a two-buffer DXGI flip-discard swap chain. It
   waits for its first confirmed frame before becoming fullscreen, topmost,
   borderless, and click-through.

The capture renderer is capped at 90 presented frames per second. Frames above
that ceiling are discarded immediately rather than queued, which limits
CryoChaos GPU work while preserving the newest available image and low latency.
This limits CryoChaos itself; it cannot guarantee total GPU utilization below
90% when Destiny or another application already saturates the GPU.

Existing effects:

- 90-degree clockwise and counterclockwise rotation
- 180-degree rotation
- Horizontal mirror
- Vertical flip

Additional shader effects:

- Old Movie — grayscale
- Negative World — inverted colors
- Low Resolution — pixelation
- RGB Split — animated chromatic aberration
- Reality Wave — animated two-axis distortion
- Kaleidoscope — rotating mirrored wedges
- Ancient Film — sepia color grading
- Comic Colors — posterized color levels
- CRT Mode — animated scanlines and flicker
- Closing In — pulsing vignette
- Breathing Zoom — animated zoom
- Signal Lost — horizontal glitches and color breakup
- Fish Eye — curved lens distortion
- Heat Vision — brightness-based thermal palette
- Chromatic Drift — animated hue rotation
- Earthquake — rapid randomized screen jolts
- Mirror Maze — repeating mirrored tiles
- Dream State — pulsing multi-sample soft focus
- Night Vision — noisy green low-light feed

### Adding another screen effect

Copy `Effects/YourScreenTransformEffect.cs`. Its `TransformMode` property is
where you select an existing effect, for example:

```csharp
protected override ScreenTransformMode TransformMode =>
    ScreenTransformMode.Kaleidoscope;
```

To add an entirely new shader, append a new explicit numeric value to
`Models/ScreenTransformMode.cs`, add a matching `mode == number` branch in
`Shaders/ScreenEffects.hlsl`, then remove `[ChaosEffectTemplate]` from your
copied effect class. Existing enum values must not be renumbered because their
numbers are passed directly to HLSL.

## Multi-input and remap effects

`MultiInputChaosEffectBase` holds multiple detected bindings concurrently.
Copy `Effects/YourMultiInputEffect.cs`, provide one alias array for each input,
rename the class and ID, and remove `[ChaosEffectTemplate]`. The included
`Diagonal Rush` effect demonstrates forward + right movement.

`Reverse Movement` demonstrates a timed physical-input swap. It resolves the
player's forward and backward keyboard bindings, installs a standard Windows
low-level keyboard hook for the effect duration, suppresses the original key,
and emits the opposite binding. The hook is active only while Destiny is the
foreground application and is removed when the effect ends.

Copy `Effects/YourInputSwapEffect.cs` to create other swaps such as left/right.
Set `FirstActionAliases` and `SecondActionAliases` to the two actions to swap,
then rename the class and ID and remove `[ChaosEffectTemplate]`.

`InputDisableChaosEffectBase` temporarily suppresses a detected keyboard key
mouse button, or individual wheel direction while Destiny is foreground.
Middle click, wheel up, and wheel down are handled as distinct inputs.
`Weapon Jammed` uses it to block
both detected fire bindings plus a left-click fallback. Keyboard and mouse
suppression hooks can now run together for one effect. Copy
`Effects/YourInputDisableEffect.cs`, change `ActionAliases`, rename its class
and ID, and remove `[ChaosEffectTemplate]` to disable another action.

Additional input chaos effects include:

- `Frozen Solid` blocks detected movement, sprint, crouch, and jump bindings,
  with standard WASD/Space/Shift/Ctrl fallbacks.
- `Scrambled WASD` creates a random no-match permutation of the player's four
  movement-direction keys for ten seconds.
- `Unskippable Ad` displays an interactive topmost ad window. Movement remains
  suppressed until the four-second timer completes and the player clicks
  **Skip ad**; cancellation and shutdown always remove the hook.
- `Ceiling Denied`, `Floor Denied`, `Vertical Lock`, and `Horizontal Lock`
  read one automatically selected physical Raw Input mouse and apply bounded
  cancellation from a separate 8 ms output pump.
- `Hyper Sensitivity` and `Sluggish Aim` use the same physical-device path to
  apply 1.75x or 0.45x sensitivity for a limited duration.
- `Sticky Key` repeatedly presses the keyboard key configured above the effect
  list. Key, delay, and press count are saved between sessions.

Ability chaos now resolves both detected Destiny bindings and conventional
Q/V/C/X fallbacks:

- `Empty The Tank`, `Aerial Disaster`, and `Bad Ability Rotation` are ordered
  macros using grenade, class ability, charged melee, air move, and jump.
- `Grenade Safety On`, `Class Ability Offline`, `Melee Battery Empty`, and
  `Grounded` disable individual ability inputs for 14 seconds.
- `Melee Battery Empty` resolves charged-melee, auto-melee, uncharged-melee,
  `melee2`, and generic melee actions, including every primary and secondary
  keyboard, mouse-button, or wheel binding.
- `Ability Blackout` disables grenade, class ability, charged melee, and air
  move together for 16 seconds.
- Ability lockout cooldowns are 50–70 seconds, and `Weapon Jammed` now lasts
  14 seconds with a 55-second cooldown.

Additional input chaos includes stackable movement and lockout sessions:

- `Grounded` now blocks both the air-move input and jump, including X and
  Space fallbacks.
- `Boots Glued Down`, `Out Of Breath`, `No Sliding`, `Magazine Stuck`,
  `Do Not Touch`, `Super Drained`, and `Hip Fire Only` disable individual
  controls for 14 seconds.
- `Bad Time To Reload`, `Accidental Super`, `Butterfingers Grenade`, and
  `Shadow Boxing` add more one-shot keybind chaos.
- `Misplaced Barricade` immediately uses the detected class ability, while
  `Airborne Arsenal` jumps, throws a grenade, equips heavy, and fires before
  landing.
- Timed keyboard/mouse hooks are composable, so stackable movement remaps and
  lockouts no longer replace one another when they overlap.
- The chaos engine allows at most three effects to be active simultaneously.
- Input lockouts resolve every matching action alias and both bindings for
  each action. For example, disabling sprint blocks hold-sprint and
  toggle-sprint primary and secondary keys together.

Mouse-look effects use standard relative `SendInput` events. They first return
focus to Destiny and wait for foreground activation before moving. This stays
external to the game process; a game or anti-cheat configuration that rejects
Windows-injected mouse input cannot be bypassed by CryoChaos.

Use **Test mouse move** in the main window to diagnose this path. It sends the
same standard relative movement while CryoChaos is focused and compares the
Windows cursor position before and after. If that test passes but Destiny's
camera still does not move, Windows accepted the event and Destiny rejected or
ignored it. The renderer does not use a driver or process injection fallback.

The Raw Input mouse effects are adapted from `SafeRawMouseEffects`. `WM_INPUT`
only records physical deltas; correction is emitted from a guarded timer with
rate limits, correction-debt carryover, an automatic timeout, and a global
**Ctrl+Alt+F12** emergency stop. CryoChaos automatically locks onto the first
non-synthetic physical mouse moved during the first raw-mouse effect and keeps
that device until it disconnects.

## Programmable repeat, sound, and QTE effects

`Effects/ProgrammableEffects.cs` contains three reusable bases:

- Derive from `RepeatingKeyChaosEffectBase`, set `VirtualKey`, and optionally
  override `PressCount`, `DelayBetweenPresses`, and `HoldDuration`.
- Derive from `SoundChaosEffectBase` and set `Sound` to a Windows sound alias,
  or set `Source` to `WaveFile` and use a relative `.wav` path.
- Derive from `QteChaosEffectBase` to change the allowed keys, prompt count,
  response time, or failure penalty. A failed standard QTE queues two random
  stackable effects while still respecting the maximum-effects setting.

Copy `YourRepeatingKeyEffect`, `YourSoundEffect`, or `YourQteEffect`, give it a
unique ID, and remove `[ChaosEffectTemplate]`. Automatic discovery adds it to
the effect library without changing `ChaosEngine`.

Custom sounds are supported. Put a PCM `.wav` file in a folder that is copied
beside the built executable (for example `Sounds/scare.wav`), then override:

```csharp
protected override string Sound => "Sounds/scare.wav";
protected override ChaosSoundSource Source => ChaosSoundSource.WaveFile;
```

The reusable `MathChallengeEffectBase` creates addition questions and, by
default, has a 1-in-100 chance to generate an algebra question. QTE and math
challenges suppress the detected movement keys until success, failure, or
timeout. `JumpScareChaosEffectBase` can also be customized with different
text, duration, and either a Windows sound alias or custom WAV file.

## Click-through video overlay effects

Put video files under `Videos` so they are copied into the build output. Copy
`YourVideoOverlayEffect`, assign a unique effect ID, remove
`[ChaosEffectTemplate]`, and change `VideoPath`. `VideoOpacity` controls the
semi-transparent video layer, `Volume` keeps or lowers the embedded audio,
`VideoStretch` controls sizing, and `Loop` controls repetition. The fullscreen
host is topmost, non-activating, transparent outside the media, and uses
`WS_EX_TRANSPARENT`, so mouse clicks continue to reach the game.

WPF `MediaElement` supports common Windows-installed media codecs. MP4/H.264
with AAC audio is the most broadly compatible choice. `VideoOpacity` fades the
whole rectangular video; per-pixel alpha video is not supported by
`MediaElement`.

## Ordered macro effects

`MacroChaosEffectBase` runs declarative steps sequentially. Supported steps:

- `MacroMouseMove(dx, dy, durationMilliseconds)`
- `MacroPressAction(aliases, holdMilliseconds, fallbackVirtualKey)`
- `MacroPressMouseButton(button, holdMilliseconds)`
- `MacroHoldTogether(aliasGroups, holdMilliseconds)`
- `MacroDelay(milliseconds)`

The included `Heavy Drop Shot` effect looks down, resolves and selects the
player's heavy-weapon binding (falling back to the `3` key), waits briefly,
then sends a left mouse click to fire.
Copy `Effects/YourMacroEffect.cs`, rename the class and effect ID, edit its
`Steps`, and remove `[ChaosEffectTemplate]`. `ChaosEngine` automatically finds
every concrete `IChaosEffect` in the project, so no registration list needs to
be edited. Template classes retain `[ChaosEffectTemplate]` so the examples do
not appear in the effect menu themselves.

The CryoChaos status overlay remains above the transformed live view, and the transformed window is click-through and non-activating.

Timed effects scale with the selected chaos level: Low uses the definition's
base duration, Normal uses 1.3x, and Chaos uses 1.75x. The overlay's Active
Effects card lists every concurrent effect with its own live countdown instead
of hiding stacked effects behind a single current-effect label.

Selection weights are normalized automatically when effects are discovered.
Regular graphic and keybind effects use weight 10, while screen-transform
effects use weight 1. This keeps ordinary effects approximately even and makes
the large screen-transform library rare as a group.

The Max Effects setting accepts values from 1 through 50. Non-stackable
effects still run alone, regardless of the configured maximum.

`RandomYouTubeEffectBase` opens one randomly selected HTTPS YouTube URL in the
user's default browser. Edit `RandomYouTubeVideoEffect.VideoUrls`, or copy
`YourRandomYouTubeEffect` and remove `[ChaosEffectTemplate]`, to provide a
custom playlist. Only `youtube.com` and `youtu.be` URLs are accepted.
The effect keeps Destiny focused while polling for a loaded YouTube-titled
browser window, activates the browser for 200 ms so media playback can start,
then restores Destiny focus.

Math Panic is stackable. Each wrong submitted answer immediately starts one
random stackable effect when a slot is available; otherwise the penalty is
queued until the math challenge finishes.

## Requirements

- Windows 11 24H2, build 26100 or newer
- .NET 8 Desktop Runtime
- x64 Direct3D 11 GPU
- Destiny 2 in Borderless Windowed mode

## Build

1. Open `CryoChaos.slnx` in Visual Studio 2022.
2. Restore NuGet packages.
3. Select `Release` and build the solution.
4. Run `bin\Release\net8.0-windows10.0.26100.0\CryoChaos.exe`.

For stable capture refresh, leave some GPU headroom. If Destiny saturates the GPU, cap its frame rate slightly below its normal maximum so Desktop Window Manager and Windows Graphics Capture can continue receiving GPU time.

## Crash logs

CryoChaos writes a session log and fatal managed-exception logs to:

```text
%LOCALAPPDATA%\CryoChaos\Logs
```

The logger records UI-thread failures, unhandled AppDomain exceptions,
unobserved task exceptions, runtime/OS/architecture information, and guarded
native-callback failures. Logs are flushed before normal crash handling
continues, and only the newest 20 log files are retained. Native fail-fast,
driver-reset, power-loss, or forced-termination failures may still require the
Windows Application event log or a crash dump because managed handlers are not
guaranteed to execute.

## Capture diagnostics

The main window includes two direct tests that bypass the chaos scheduler and
the fullscreen effect overlay:

- **Test game capture** copies only the Destiny window into a normal resizable
  preview window.
- **Test monitor capture** copies the monitor containing Destiny. A recursive
  hall-of-mirrors image is expected when the diagnostic window is visible on
  that monitor and proves that monitor capture is working.

Each diagnostic reports the number of frames presented, current FPS, capture
dimensions, and output dimensions. If it still reports zero frames after three
seconds, the status turns orange and identifies the capture stage as stalled.
