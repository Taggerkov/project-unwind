# ASys: Voiceline Subsystem

Priority-queued character dialogue management built on top of `AudioManager`.
Handles interruption, ordering, subtitle display, localisation, pause/resume, and runtime volume control.
Game systems request playback by priority; the subsystem decides whether to play immediately, interrupt
the current voiceline, or enqueue behind higher-priority entries.

---

## Components

### VoicelineEvent

A `ScriptableObject` asset pairing an `AudioEvent` clip with voiceline-specific metadata.
The inner `AudioEvent` must have `AudioCategory.Voice`.

| Field             | Type                | Description                                                                               |
|-------------------|---------------------|-------------------------------------------------------------------------------------------|
| `AudioEvent`      | `AudioEvent`        | The voice clip. Must use `AudioCategory.Voice`.                                           |
| `SubtitleKey`     | `string`            | Localisation key resolved at playtime. Empty keys produce no subtitle.                    |
| `Duration`        | `float`             | Optional clip length in seconds. For UI progress bars only; not used for playback timing. |
| `DefaultPriority` | `VoicelinePriority` | Priority used when calling `Play(VoicelineEvent)` without an explicit override.           |

Create via right-click: **Create → Unwind → Audio → Voiceline Event**.

### VoicelinePriority

| Value      | Integer | Interrupts           |
|------------|---------|----------------------|
| `Normal`   | 0       | Nothing.             |
| `High`     | 1       | `Normal`.            |
| `Critical` | 2       | `Normal` and `High`. |

Interruption is **strictly greater than**: a `High` voiceline does not interrupt another `High`.

### VoicelineManager

Plain C# singleton injected by Reflex. Owns the priority queue, interruption rules, subtitle wiring,
and the current-line policy state, and delegates single-line playback (handle lifecycle,
pause/resume/stop, the completion watcher, and the volume `VolumeFader`) to a composed `AudioStream`
scoped to `AudioCategory.Voice`. Depends on `AudioManager` and `LanguageSystem`. Callers never interact
with the queue, backend, or subtitle component directly.

The drain policy is wired by subscribing to `AudioStream.Completed`, which fires only on natural end:
the handler fires `OnVoicelineCompleted` and plays the next queued line.

### LanguageSystem / LocalizationUtility

`LanguageSystem` tracks the current runtime language (defaults to `English`). Set it via
`SetLanguage(Language)`. `LocalizationUtility.GetLocalizedText(key, language)` resolves a subtitle
key to display text. The current implementation is a stub that returns the key unchanged; replace
it with a real localisation database in production.

---

## Lifecycle

```
Play(event, priority)
  → if idle: PlayImmediate
  → if priority > current: StopCurrent → OnVoicelineInterrupted → PlayImmediate
  → otherwise: Enqueue(priority)

PlayImmediate(event, priority)
  → AudioStream.Play(audioEvent)
  → if false (not preloaded): OnVoicelineFailed → PlayNextQueued  (skips past failed entry)
  → otherwise: set policy state, DisplaySubtitle, OnVoicelineStarted

AudioStream.Completed  (natural end only)
  → StopCurrent (resets policy state) → OnVoicelineCompleted → PlayNextQueued

PlayNextQueued
  → if queue empty: OnQueueEmpty
  → otherwise: PlayImmediate(next)
```

`AudioStream` owns the completion watcher and cancels it before any explicit stop, skip, pause, or
interruption, so `Completed` is raised only on natural end and no spurious queue advance occurs.
`StopCurrent` simply calls `AudioStream.Stop()` and resets the voiceline policy state (current line,
priority, subtitle).

---

## Public API

### Preload / Unload

| Method                                                         | Returns   | Description                                                                             |
|----------------------------------------------------------------|-----------|-----------------------------------------------------------------------------------------|
| `PreloadAsync(VoicelineEvent, CancellationToken)`              | `UniTask` | Loads the clip into the backend cache. Must complete before `Play`.                     |
| `PreloadAsync(IEnumerable<VoicelineEvent>, CancellationToken)` | `UniTask` | Loads all clips in parallel. Null or missing `AudioEvent` entries are skipped silently. |
| `Unload(VoicelineEvent)`                                       | `void`    | Releases the clip from memory. Null or missing `AudioEvent` logs a warning.             |
| `Unload(IEnumerable<VoicelineEvent>)`                          | `void`    | Releases all clips. Null or missing `AudioEvent` entries are skipped silently.          |

### Playback Control

| Method                                    | Returns | Description                                                                                                                       |
|-------------------------------------------|---------|-----------------------------------------------------------------------------------------------------------------------------------|
| `Play(VoicelineEvent)`                    | `void`  | Requests playback using `VoicelineEvent.DefaultPriority`.                                                                         |
| `Play(VoicelineEvent, VoicelinePriority)` | `void`  | Requests playback at an explicit priority. Plays immediately if idle, interrupts if strictly higher priority, otherwise enqueues. |
| `Stop()`                                  | `void`  | Stops current playback and clears the entire queue.                                                                               |
| `Skip()`                                  | `void`  | Stops current playback and starts the next queued entry. Also works while paused. No-op if idle.                                  |
| `Restart()`                               | `void`  | Stops and replays the current voiceline from the beginning at the same priority. Also works while paused. No-op if idle.          |
| `Clear()`                                 | `void`  | Clears all queued voicelines without stopping the currently playing one.                                                          |
| `Pause()`                                 | `void`  | Pauses the current voiceline, preserving position. No-op if not playing.                                                          |
| `Resume()`                                | `void`  | Resumes a paused voiceline from where it was paused. No-op if not paused.                                                         |
| `TogglePause()`                           | `void`  | Pauses if playing; resumes if paused. No-op if idle.                                                                              |

### Volume

| Method                                            | Returns   | Description                                                                                                                                                 |
|---------------------------------------------------|-----------|-------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `SetVolume(float)`                                | `void`    | Sets the master volume for all voice playback immediately. Cancels any in-flight fade. Clamped to ≥ 0.                                                      |
| `FadeVolumeToAsync(float target, float duration)` | `UniTask` | Linearly interpolates voice volume to `target` over `duration` seconds. A new call cancels any running fade. Zero or negative duration applies immediately. |

### State Inspection

| Method / Property                 | Returns                         | Description                                                                                                                |
|-----------------------------------|---------------------------------|----------------------------------------------------------------------------------------------------------------------------|
| `IsPlaying`                       | `bool`                          | True while a voiceline is actively playing (not paused).                                                                   |
| `IsPaused`                        | `bool`                          | True while playback is paused.                                                                                             |
| `IsIdle`                          | `bool`                          | True when nothing is playing, paused, or queued.                                                                           |
| `QueueCount`                      | `int`                           | Number of voicelines waiting in the queue.                                                                                 |
| `Volume`                          | `float`                         | Current master volume for voice playback. Reflects in-progress fades.                                                      |
| `CurrentVoiceline`                | `VoicelineEvent?`               | The voiceline currently playing or paused. Null when idle.                                                                 |
| `CurrentPriority`                 | `VoicelinePriority?`            | Priority of the current voiceline. Null when idle.                                                                         |
| `GetQueuedVoicelines()`           | `IReadOnlyList<VoicelineEvent>` | Snapshot of queued voicelines in priority order.                                                                           |
| `RemoveFromQueue(VoicelineEvent)` | `bool`                          | Removes the first matching entry from the queue. No effect on the currently playing voiceline. Returns false if not found. |
| `IsQueued(VoicelineEvent)`        | `bool`                          | Returns true if the event is present anywhere in the queue.                                                                |

### UI Wiring

| Method                                | Returns | Description                                                                                                                  |
|---------------------------------------|---------|------------------------------------------------------------------------------------------------------------------------------|
| `SetSubtitleDisplay(TextMeshProUGUI)` | `void`  | Binds the TMP component that receives subtitle text. Call once from a scene `MonoBehaviour`. Pass null to disable subtitles. |

### Events

| Event                    | Signature                | Raised when                                                         |
|--------------------------|--------------------------|---------------------------------------------------------------------|
| `OnVoicelineStarted`     | `Action<VoicelineEvent>` | A voiceline begins playing.                                         |
| `OnVoicelineCompleted`   | `Action<VoicelineEvent>` | A voiceline ends naturally (not via `Stop` or `Skip`).              |
| `OnVoicelineInterrupted` | `Action<VoicelineEvent>` | A voiceline is stopped by a higher-priority incoming one.           |
| `OnVoicelineFailed`      | `Action<VoicelineEvent>` | A voiceline could not be played because its clip was not preloaded. |
| `OnQueueEmpty`           | `Action`                 | The last queued voiceline has finished and no more are queued.      |

---

## Priority and Interruption

Interruption is evaluated on every `Play` call when something is already playing or paused.
A new voiceline interrupts the current one only if its priority is **strictly greater**:

| Incoming \ Current | Normal    | High      | Critical |
|--------------------|-----------|-----------|----------|
| Normal             | Enqueue   | Enqueue   | Enqueue  |
| High               | Interrupt | Enqueue   | Enqueue  |
| Critical           | Interrupt | Interrupt | Enqueue  |

`OnVoicelineInterrupted` fires with the displaced voiceline before the incoming one starts.
The same rules apply while paused — a higher-priority `Play` interrupts even a paused voiceline.

---

## Queue

The underlying `PriorityQueue<QueuedVoiceline>` maintains FIFO order within each priority tier.
Two `High` voicelines enqueued in order play in that order.

If a dequeued entry fails to play (preload miss), `OnVoicelineFailed` fires and the queue drains
automatically to the next entry. If all remaining entries fail, `OnQueueEmpty` fires once the queue
is exhausted. Stack depth equals queue length; safe for realistic voiceline counts.

---

## Pause / Resume Invariant

The watcher is cancelled before pausing, which prevents it from seeing the paused handle as stopped and
spuriously advancing the queue, and is restarted on resume. This discipline lives in `AudioStream`, so
`VoicelineManager.Pause()` and `Resume()` delegate to `AudioStream.Pause()` and `AudioStream.Resume()`.
Pause and resume act on the single active handle, not the whole `Voice` category.

```
Pause():  AudioStream.Pause()  → cancels watcher, pauses the handle
Resume(): AudioStream.Resume() → resumes the handle, restarts the watcher
```

---

## Subtitle Display

Subtitles are written to a `TextMeshProUGUI` component bound via `SetSubtitleDisplay`. When a voiceline
starts, the subtitle key is resolved via `LocalizationUtility.GetLocalizedText(key, language)` using
`LanguageSystem.CurrentLanguage` and written to the component. The display is cleared when playback stops
regardless of reason (stop, skip, interrupt, or natural completion).

Subtitle rendering is a no-op when:

- No display has been bound (`SetSubtitleDisplay` was never called or was called with null).
- `VoicelineEvent.SubtitleKey` is null or empty.

---

## Cancellation

Two `CancellationTokenSource` instances govern async operations, both owned below `VoicelineManager`:

| Field         | Owner         | Cancels                                                                                                             |
|---------------|---------------|---------------------------------------------------------------------------------------------------------------------|
| watcher token | `AudioStream` | The active completion watcher. Cancelled on every stop, skip, pause, interrupt, and before replaying via `Restart`. |
| `_fadeCts`    | `VolumeFader` | The active `FadeVolumeToAsync` task. Cancelled on any new fade call or `SetVolume`. Disposed on `Dispose`.          |

---

## Wiring

`VoicelineManager` is registered in `RootInstaller` alongside `LanguageSystem`. Reflex disposes it
before `AudioManager` so `StopAll(Voice)` is safe to call from `Dispose`.

Bind the subtitle display from a scene `MonoBehaviour` after Reflex injection:

```csharp
[Inject] private VoicelineManager _voicelineManager;

private void Start()
{
    _voicelineManager.SetSubtitleDisplay(subtitleText);
}
```

Preload all voicelines needed for a scene or encounter before any `Play` call:

```csharp
await _voicelineManager.PreloadAsync(combatVoicelines, cancellationToken);
```

Unload when the encounter ends:

```csharp
_voicelineManager.Unload(combatVoicelines);
```

---

## Constraints

- All voicelines must be preloaded before `Play`. Unpreloaded entries fire `OnVoicelineFailed` and are skipped.
- `VoicelineEvent.AudioEvent` must use `AudioCategory.Voice`. Other categories are not validated at queue time but will
  be routed to the wrong category bucket at playback.
- `SetSubtitleDisplay` must be called from a scene `MonoBehaviour`; it is not injected.
- `Skip`, `Restart`, and `TogglePause` are no-ops when `IsIdle` is true.
- `Restart` fires `OnVoicelineStarted` again for the same event; it does not fire `OnVoicelineCompleted` or
  `OnVoicelineInterrupted` for the restarted instance.
- `Dispose` stops all active voice playback via `StopAll(Voice)`, clears the queue, and cancels any in-flight fade.
