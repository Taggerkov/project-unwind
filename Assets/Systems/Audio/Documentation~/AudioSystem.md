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

An `AudioEvent` is a Unity `ScriptableObject` asset representing the default playback configuration for
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
Extends `IDisposable`: the backend is responsible for terminating its own handles on disposal.

### IAudioHandle

Represents a single live playback instance. Exposes per-instance control over pause, resume, stop,
volume, and speed. When playback ends, either via explicit `Stop` or natural completion, `OnReleased`
is raised exactly once and the handle becomes invalid.

It also exposes read-only state for inspection: `Volume`, `Speed`, `Category`, `IsLooping`, `IsPlaying`,
`IsPaused`, `Time` (current position), and `Length` (clip length). `AudioManager.TryGetSnapshot` reads these
into an `AudioPlaybackSnapshot`.

---

## Implementation Layer

Backends implement `IAudioService` and `IAudioHandle` entirely within their own scope. No engine types,
pool references, or backend-specific objects are ever visible outside the implementation surface.

Validation and clamping of values like volume and speed are the backend's responsibility, as constraints
differ between implementations. The BuiltIn backend clamps category volume and speed to a zero minimum;
per-instance `SetVolume` is also clamped to zero, while per-instance `SetSpeed` accepts any value
including negative (reversal where supported).

| Backend | Status  | Description                         |
|---------|---------|-------------------------------------|
| BuiltIn | Active  | Unity `AudioSource` implementation. |
| FMOD    | Planned | FMOD Studio implementation.         |

Swapping backends requires changing the `AudioBackend` value in the `AudioSettings` asset. Nothing else changes.

See [ASysBuiltIn.md](ASysBuiltIn.md) for full BuiltIn backend documentation.

---

## AudioManager

The single surface all game code talks to. Registered as a singleton in the Reflex container and
injected wherever audio is needed. It owns UUID-to-handle tracking, constructs the backend specified
in `AudioSettings`, and translates `AudioEvent` assets into `AudioRequest` descriptors. The returned
`Guid` is the only thing game code retains after a `Play` call.

On disposal, `AudioManager` unsubscribes its release listeners and drops its UUID map, then delegates
to the backend. The backend terminates all active handles and releases engine resources.

Backend selection is configured via the `AudioSettings` asset dragged into `RootInstaller`.

### Preload / Unload

| Method                                                     | Returns   | Description                                                                                                                            |
|------------------------------------------------------------|-----------|----------------------------------------------------------------------------------------------------------------------------------------|
| `PreloadAsync(AudioEvent, CancellationToken)`              | `UniTask` | Loads the clip into the backend cache. Must complete before `Play`. No-op if already cached.                                           |
| `PreloadAsync(IEnumerable<AudioEvent>, CancellationToken)` | `UniTask` | Loads all clips in parallel. Null entries are skipped with a warning.                                                                  |
| `Unload(AudioEvent)`                                       | `bool`    | Releases the clip from memory. Active handles playing that clip continue until natural completion. Returns false if the event is null. |

### Playback

| Method                                            | Returns             | Description                                                                                                                                                                                                             |
|---------------------------------------------------|---------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Play(AudioEvent)`                                | `Guid`              | Plays the event with its default values. Returns `Guid.Empty` if the event is null or the clip was not preloaded.                                                                                                       |
| `Stop(Guid)`                                      | `bool`              | Stops playback immediately and releases the handle. Returns false if not active.                                                                                                                                        |
| `Pause(Guid)`                                     | `bool`              | Pauses playback, preserving position. Returns false if not active.                                                                                                                                                      |
| `Resume(Guid)`                                    | `bool`              | Resumes a paused playback. Returns false if not active.                                                                                                                                                                 |
| `SetVolume(Guid, float)`                          | `bool`              | Overrides per-instance volume at runtime. Clamped to zero minimum. Returns false if not active.                                                                                                                         |
| `SetSpeed(Guid, float)`                           | `bool`              | Overrides per-instance speed (pitch multiplier) at runtime. Negative values reverse playback where supported. Returns false if not active.                                                                              |
| `IsPlaying(Guid)`                                 | `bool`              | Returns true if the playback is active and not paused.                                                                                                                                                                  |
| `IsPaused(Guid)`                                  | `bool`              | Returns true if the playback is currently paused.                                                                                                                                                                       |
| `AwaitCompletionAsync(Guid, CancellationToken)`   | `UniTask`           | Completes when the playback ends naturally or via `Stop`. Completes immediately if the handle is no longer active. Cancel the token before calling `Stop` to distinguish the two cases via `SuppressCancellationThrow`. |
| `GetClipName(Guid)`                               | `string`            | Returns the `AudioEvent` name for the given UUID, or a short UUID fallback if no longer tracked.                                                                                                                        |
| `GetVolume(Guid, float fallback = 1)`             | `float`             | Returns the handle volume layer (excludes the category multiplier), or `fallback` if not active. Used by `VolumeFader` to read the live start value of a fade.                                                          |
| `TryGetSnapshot(Guid, out AudioPlaybackSnapshot)` | `bool`              | Captures a read-only `AudioPlaybackSnapshot` (name, category, playing/paused/looping, volume, speed, time, length). False if not active.                                                                                |
| `ActiveUuids`                                     | `IEnumerable<Guid>` | All currently active playback UUIDs.                                                                                                                                                                                    |

### Category Control

| Method                                    | Returns | Description                                                                                             |
|-------------------------------------------|---------|---------------------------------------------------------------------------------------------------------|
| `StopAll(AudioCategory)`                  | `void`  | Stops all active sounds in the category.                                                                |
| `PauseAll(AudioCategory)`                 | `void`  | Pauses all active sounds in the category.                                                               |
| `ResumeAll(AudioCategory)`                | `void`  | Resumes all paused sounds in the category.                                                              |
| `SetCategoryVolume(AudioCategory, float)` | `void`  | Sets the master volume multiplier for the category. Clamped to ≥ 0. Applied live to all active handles. |
| `SetCategorySpeed(AudioCategory, float)`  | `void`  | Sets the master speed multiplier for the category. Clamped to ≥ 0. Applied live to all active handles.  |
| `GetCategoryVolume(AudioCategory)`        | `float` | Returns the current master volume multiplier for the category.                                          |
| `GetCategorySpeed(AudioCategory)`         | `float` | Returns the current master speed multiplier for the category.                                           |

---

## Playback Engine (AudioStream)

`AudioStream` is a single-track playback engine scoped to one `AudioCategory`, layered over `AudioManager`.
It owns the live handle, the play/pause/resume/stop lifecycle, a category `VolumeFader`, and a completion
watcher that raises `Completed` only on natural end of playback (never on explicit stop, pause, or
replacement). Higher-level systems compose an `AudioStream` and supply their own "what plays next" policy by
subscribing to `Completed`.

The watcher's cancellation discipline (cancel the watcher before any explicit stop, pause, or replacement so a
stop never triggers a spurious completion) lives inside `AudioStream`, so the music and voiceline subsystems
cannot mis-mirror it. Both subsystems delegate playback to an `AudioStream` and add only their own sequencing
or queuing on top.

---

## Music Subsystem

The music subsystem sits on top of `AudioManager` (via an `AudioStream`) and manages playlist-based background
music. See [ASysMusic.md](ASysMusic.md) for full documentation.

```csharp
_musicManager.ActivatePlaylist(PlaylistType.Combat).Forget();
_musicManager.NextTrack();
_musicManager.Pause();
_musicManager.Resume();
_musicManager.Stop();
```

---

## Voiceline Subsystem

The voiceline subsystem sits on top of `AudioManager` (via an `AudioStream`) and manages priority-queued
character dialogue with subtitle display and localisation support.
See [ASysVoiceline.md](ASysVoiceline.md) for full documentation.

```csharp
await _voicelineManager.PreloadAsync(voicelineEvent, cancellationToken);
_voicelineManager.Play(voicelineEvent);
_voicelineManager.Play(voicelineEvent, VoicelinePriority.Critical);
_voicelineManager.Pause();
_voicelineManager.Resume();
_voicelineManager.Stop();
```

---

## Usage

```csharp
// Preload a single clip before play
await _audioManager.PreloadAsync(myAudioEvent, cancellationToken);

// Preload a batch in parallel
await _audioManager.PreloadAsync(new[] { eventA, eventB, eventC }, cancellationToken);

// Play returns a UUID
var uuid = _audioManager.Play(myAudioEvent);

// Control by UUID
_audioManager.Pause(uuid);
_audioManager.Resume(uuid);
_audioManager.SetVolume(uuid, 0.5f);
_audioManager.SetSpeed(uuid, 0.8f);
_audioManager.Stop(uuid);

// Await natural completion (cancel the token before Stop to distinguish the two cases)
await _audioManager.AwaitCompletionAsync(uuid, cancellationToken);

// Category control
_audioManager.StopAll(AudioCategory.Music);
_audioManager.PauseAll(AudioCategory.Sfx);
_audioManager.ResumeAll(AudioCategory.Sfx);
_audioManager.SetCategoryVolume(AudioCategory.Ambient, 0.5f);
_audioManager.SetCategorySpeed(AudioCategory.Sfx, 0.3f); // e.g. hitstop
_audioManager.GetCategoryVolume(AudioCategory.Music);
_audioManager.GetCategorySpeed(AudioCategory.Music);

// Unload when no longer needed
_audioManager.Unload(myAudioEvent);
```

---

## Diagnostics and Editor Tooling

`AudioDiagnostics` is a development-only logger for authoring and misuse warnings (null events, redundant
preloads, playlist validation, undersized pool). Its `Warn` and `Error` methods are marked
`[Conditional("UNITY_EDITOR")]` and `[Conditional("DEVELOPMENT_BUILD")]`, so calls and their argument
evaluation are stripped from release player builds. Each message is auto-tagged with its layer
(`Audio`, `Audio/Music`, `Audio/Voiceline`, `Audio/BuiltIn`) and calling type via `[CallerFilePath]`.
Genuine runtime failures (a real load failure, a caught playback exception) stay as always-on `Debug.LogError`.

`AudioManager.TryGetBackendStats(out AudioBackendStats)` reports backend internals (active and created sources,
configured pool size, pool-grew flag, cached clips, in-flight loads). It is compiled only in the editor and
development builds, and reaches the backend through the optional `IAudioDiagnosticsSource` capability rather than
coupling to a concrete backend.

The **Audio Control Centre** editor window (`Unwind → Audio → Manager`) inspects and drives the system at runtime:
Home (config + backend stats), Live (snapshot-driven handle list), Music, Voice, Settings (per-category volume,
speed, mute, solo), and Audition (preview AudioEvent and AudioSheet assets with an explicit
Preload / Play / Stop / Unload lifecycle).

---

## Constraints

- Clips must be preloaded before `Play` is called. Calling `Play` with an unpreloaded key returns `Guid.Empty` and logs
  an error.
- The system is 2D only. No spatial audio support.
- Looping sounds must be stopped explicitly. Failure to do so holds the rented `AudioSource` until `Dispose`.
- The returned UUID is the only valid reference to a playback instance outside the audio assembly.
- Category volume and speed are clamped to zero minimum. Per-instance volume is also clamped; per-instance speed accepts
  any value including negative.
