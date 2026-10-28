# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls and
what the tests check; this file covers how the code is put together and the rules that keep it working.

## What this is

A vertical-scrolling air-war shooter in the style of Capcom's 1984 arcade game 1942, in C# on .NET 10 and
Avalonia 12. One self-contained executable per platform (Windows, macOS and Linux, x64 and ARM). Everything in it
is original: the planes are drawn from text sprites in code, and the sounds and tunes are synthesised by
`tools/make_sounds.py`.

## Commands

```bash
dotnet build AVA1942.slnx                         # the SDK version is in global.json (10.0.400 or later)
dotnet test AVA1942.slnx                          # every test runs headless: no window opens
dotnet run --project src/AVA1942                  # play it; --no-audio for silence
AVA1942_SHOTS=$PWD/Images dotnet test tests/AVA1942.Tests --filter ScreenshotTests   # refresh the README's pictures
python3 tools/make_sounds.py                      # rebuild src/AVA1942/Sounds (standard-library Python only)
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types and implicit usings for every
project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVA1942/Engine/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states (title, attract, play, high scores) and the stage phases (take-off,
    flight, landing, tally).
  - `GameEngine.Play.cs` steps the flight: the player's plane, wingmen, waves, bombers, the great bomber and
    power-ups.
  - `Stages.cs` holds the eight stages; `Entities.cs` the enemy kinds and `Controls`; `Geometry.cs` the
    playfield measures and the fixed step.
  - `AttractPilot.cs` is the computer pilot, used by the attract mode and the tests.
  - `InputHandler.cs` (keys) and `ControllerManager.cs` (SDL2 gamepads) produce `Controls`.
  - `HighScoreTable.cs`, `InitialsEntry.cs` and `SoundManager.cs` do what their names say.
- `src/AVA1942/Views/`: `GameCanvas` (split over partial files) draws the 1024x768 frame and runs the 60 Hz
  timer; `Sprites.cs` holds the plane drawings.
- `src/AVA1942/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `tests/AVA1942.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`: gameplay, screenshot and Shift-release tests.

## Rules that keep it working

- **Fixed steps.** `GameEngine.Update` advances the game in fixed 1/60 s steps (`Field.Step`), independent of the
  frame rate, so a seed always plays the same game. Draw random numbers from the engine's seeded `_rng`, never
  from `Random.Shared`.
- **Engine first, drawing second.** Gameplay belongs in `Engine/`, where tests can reach it without a window.
  `Views/` only reads the engine and draws it.
- **Input goes through `InputHandler`.** Up/down on keys and stick must produce the same `dy` sign the engine
  expects (negative is up the screen). Test new controls through `InputHandler`, not by setting `Controls`
  directly.
  - Keys are tracked by `PhysicalKey`, because on some Linux setups Avalonia reports the release of Shift as
    CapsLock. `ShiftReleaseTests` covers this.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests never touch the player's files.** High scores live in the app-data folder (`AVA1942`); tests load the
  table from a temporary folder and run the engine with `Silent = true`.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls, the test list) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push. It then publishes self-contained single-file builds for
win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as workflow artifacts.
Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a proper release.
