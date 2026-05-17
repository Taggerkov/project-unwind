# Audio System v2.0

A modular audio system for Unity built around a single authoritative surface. Game systems interact
exclusively with `AudioManager` using `AudioEvent` assets as sound descriptors and UUIDs as playback tokens.

## Architecture

![Audio System Domain Model](ASysDomain.png)

The system is divided into three distinct concerns: authoring, contract, and implementation. Each layer
knows only what it needs to. Game code never reaches past `AudioManager`.

---

## Authoring Layer

### AudioEvent

A `AudioEvent` is a Unity `ScriptableObject` asset representing the default playback configuration for
a single audio clip. It holds an Addressables asset GUID, a category, volume, speed, and loop state.
Fields are open for Inspector editing and can be overridden before being passed to `AudioManager`.

Game systems hold `AudioEvent` references solely to pass them to `AudioManager.Play`.

---

## Contract Layer

### AudioRequest

When an `AudioEvent` is to be played, it is first converted into an `AudioRequest` via
`AudioRequest.FromAudioEvent`. This is a lightweight, immutable struct with no Unity dependencies.
It carries the same data as the `AudioEvent` but in a form that is engine-agnostic and safe to pass
across backend boundaries using the `in` modifier to avoid copies.

Backends receive only `AudioRequest`. The `AudioEvent` asset is never visible below the contract layer.

### IAudioService

The playback contract that every backend implements. It defines preloading, unloading, playback, and
category-level control. It is entirely internal to the audio assembly and invisible to game code.

### IAudioHandle

Represents a single live playback instance. Exposes per-instance control over pause, resume, stop,
volume, and speed. When playback ends, either via explicit `Stop` or natural completion, `OnReleased`
is raised exactly once and the handle becomes invalid.

---

## Implementation Layer

Backends implement `IAudioService` and `IAudioHandle` entirely within their own scope. No engine types,
pool references, or backend-specific objects are ever visible outside the implementation surface.

Validation and clamping of values like volume and speed are the backend's responsibility, as constraints
differ between implementations.

| Backend | Status  | Description                         |
|---------|---------|-------------------------------------|
| BuiltIn | Active  | Unity `AudioSource` implementation. |
| FMOD    | Planned | FMOD Studio implementation.         |

Swapping backends requires changing the `AudioBackend` value in the `AudioSettings` asset. Nothing else changes.

---

## AudioManager

The single surface all game code talks to. Registered as a singleton in the Reflex container and
injected wherever audio is needed. It owns UUID-to-handle tracking, constructs the backend specified
in `AudioSettings`, and translates `AudioEvent` assets into `AudioRequest` descriptors. The returned
`Guid` is the only thing game code retains after a `Play` call.

Backend selection is configured via the `AudioSettings` asset dragged into `RootInstaller`.

---

## Usage

```csharp
// Preload before play
await _audioManager.PreloadAsync(myAudioEvent);

// Play returns a UUID
var uuid = _audioManager.Play(myAudioEvent);

// Control by UUID
_audioManager.Pause(uuid);
_audioManager.Resume(uuid);
_audioManager.SetVolume(uuid, 0.5f);
_audioManager.SetSpeed(uuid, 0.8f);
_audioManager.Stop(uuid);

// Category control
_audioManager.StopAll(AudioCategory.Music);
_audioManager.SetCategoryVolume(AudioCategory.Ambient, 0.5f);

// Unload when no longer needed
_audioManager.Unload(myAudioEvent);
```

---

## Constraints

- Clips must be preloaded before `Play` is called.
- The system is 2D only. No spatial audio support.
- Looping sounds must be stopped explicitly. Failure to do so leaks the underlying source.
- The returned UUID is the only valid reference to a playback instance outside the audio assembly.