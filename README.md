# CryoChaos Keybind Mouse Movement Patch

Replace these files in your current project:

- `Services/KeyboardInputService.cs`
- `Effects/KeybindEffects.cs`
- `Services/ChaosEngine.cs`

Then clean and rebuild the solution.

## Added input methods

- `MoveMouseRelative(int deltaX, int deltaY)`
- `MoveMouseSmoothAsync(int totalDeltaX, int totalDeltaY, TimeSpan duration, CancellationToken cancellationToken)`

Directions:

- Positive X: right
- Negative X: left
- Positive Y: down
- Negative Y: up

## Added effects

- Look Left
- Look Right
- Look Up
- Look Down
- Camera Jolt

Tune the movement amounts in `DirectionalLookEffectBase.GetMovement` and `CameraJoltEffect.GetMovement`.
