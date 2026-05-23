# ASys: BuiltIn Implementation

Unity `AudioSource`-based implementation of `IAudioService` and `IAudioHandle`.
Clip resolution uses Addressables via asset GUIDs. `AudioSource` lifecycle is managed by an internal pool.
Nothing in this layer is visible outside `Runtime/BuiltIn/`.

---

## Folder Structure

```
Runtime/
  BuiltIn/
    Internal/
      UnityAudioPool.cs
      UnityAudioBank.cs
    AudioHandle.cs
    BuiltInAudio.cs
    ICategoryProvider.cs
```

---

## Components

### UnityAudioBank

Resolves asset GUIDs to `AudioClip` assets via Addressables. Owns the preload cache and all
Addressables handle lifetimes. Deduplicates concurrent preload requests for the same key so
multiple callers preloading the same clip at the same time produce only one load operation.

Concurrent dedup uses a `Dictionary<string, UniTaskCompletionSource>` keyed by address. The first
caller for a key runs the load and resolves the shared completion source; concurrent callers await
`source.Task`. A `UniTaskCompletionSource` is used (not the load `UniTask` itself) because it supports
multiple concurrent awaiters, whereas awaiting a single `UniTask` twice throws.

A preload request for a key that is already cached or in flight is a no-op that emits a development-only
warning via `AudioDiagnostics`: clips are expected to be preloaded by a single owner, so a redundant
request is a wasted call and risks one owner unloading a clip another still depends on.

Unloading a clip releases the Addressables handle and removes it from the cache. Any
`AudioSource` already playing that clip holds its own reference and continues until natural
completion.

If `Unload` is called for a key that is still loading, the release is deferred: the key is added
to `_pendingUnloads` and `LoadAsync` releases the handle instead of caching it once the load
completes. This prevents ghost cache entries when unload races ahead of an in-flight load.

`Dispose` releases all cached Addressables handles.

### UnityAudioPool

Wraps `UnityEngine.Pool.ObjectPool<AudioSource>`. Creates a dedicated `GameObject` marked
`DontDestroyOnLoad` to host pooled sources and run coroutines. Sources are fully reset on
return via the pool's release callback, including `clip`, `volume`, `pitch`, `loop`,
`spatialBlend`, and `outputAudioMixerGroup`. The pool also serves as the coroutine host for
one-shot cleanup, forwarding `StartCoroutine` and `StopCoroutine` to its host `MonoBehaviour`.

`Dispose` calls `ObjectPool.Dispose` (destroys pooled components) and then destroys the host
`GameObject`.

In the editor and development builds the pool tracks how many sources it has created and warns once,
via `AudioDiagnostics`, when creation exceeds the configured `AudioSettings.PoolSize` (a hint to raise
the pool size to cover peak concurrent sounds and avoid runtime allocations). This tracking state and its
counters are fenced behind `#if UNITY_EDITOR || DEVELOPMENT_BUILD` so nothing survives a release build.
The pool also exposes active and created counts (development builds only) that `BuiltInAudio` reads for
backend diagnostics.

### ICategoryProvider

Read-only interface exposing the current volume and speed multipliers per category.
Implemented by `BuiltInAudio` and passed to `AudioHandle` at construction. Gives the handle
access to category state without exposing the full `BuiltInAudio` surface.

### AudioHandle

BuiltIn playback handle wrapping a rented `AudioSource`. Manages the full playback lifecycle
for a single audio instance including pause, resume, volume, speed, and cleanup.

Playback values are organised into three layers:

- **Handle layer**: per-instance volume and speed, owned by the handle.
- **Category layer**: multipliers queried live from `ICategoryProvider` at write time.
- **Source layer**: the final combined result written to `AudioSource`. The single write point.

`SetVolume` clamps the handle layer to a zero minimum before applying. `SetSpeed` accepts any
value including negative (reverses playback per the contract); the cleanup coroutine guards
against zero or negative speed products by releasing the handle immediately rather than
scheduling an infinite or backwards wait.

`SetSpeed` and `SetVolume` update the handle layer and trigger an immediate recalculation.
Category changes are triggered by `BuiltInAudio`, which calls `ApplyVolume` and `ApplySpeed`
on each affected handle after updating its own category dictionaries.

One-shot clips schedule their own cleanup via a coroutine that calculates remaining duration
from current speed and playback position, then waits with `WaitForSecondsRealtime`. Realtime is
used because `AudioSource` playback ignores `Time.timeScale`, so a paused or slowed game would
otherwise leak handles whose clips have already finished. The coroutine is cancelled and rescheduled
whenever speed changes while playing, and always rescheduled on resume to account for any speed
changes accumulated during pause.

Beyond playback control, the handle exposes read-only state for inspection: `Volume`, `Speed`,
`Category`, `IsLooping`, `IsPlaying`, `IsPaused`, `Time` (current position, zero once released), and
`Length` (clip length). `AudioManager.TryGetSnapshot` reads these into an `AudioPlaybackSnapshot`.

Looping clips have no coroutine. Cleanup is always explicit via `Stop()`. A looping handle
left without a `Stop()` call holds its rented `AudioSource` until `BuiltInAudio.Dispose`.

On release, whether via `Stop()` or natural coroutine completion, the handle raises `OnReleased`,
invokes its `_onStopped` callback to remove itself from `BuiltInAudio._activeHandles`, returns
the source to the pool, and nulls its internal reference.

### BuiltInAudio

Single entry point for the BuiltIn backend. Owns and orchestrates `UnityAudioBank` and
`UnityAudioPool`, constructing them internally from `AudioSettings`. Constructed internally by
`AudioManager`; not registered in the Reflex container directly. Implements `IAudioService` as the
backend surface and `ICategoryProvider` to give `AudioHandle` read access to category state without
exposing the full `BuiltInAudio` surface. Callers never interact with internal types directly.

Tracks active handles per category in a `Dictionary<AudioCategory, List<AudioHandle>>` for
bulk operations. On category change, `BuiltInAudio` iterates only the affected category and
triggers recalculation on each handle. The `OnHandleStopped` callback removes a handle from
its category list in O(1) using the handle's own `Category` property.

Category volume and speed multipliers are clamped to a zero minimum at the setter. Values
above 1 are accepted for volume (backend-dependent amplification) and above 1 for speed
(faster pitch).

`Play` requires the requested key to be preloaded. If `TryGet` returns false, an
`InvalidOperationException` is thrown. The caller is responsible for preloading before play.

`Dispose` stops all active handles by iterating `_activeHandles` backward (safe because
`Stop` triggers `OnHandleStopped` which removes from the same list), then disposes the pool
and the bank. The backend is the disposal owner: `AudioManager.Dispose` only drops its UUID
map and event subscriptions, then delegates to the backend.

There is no polling and no tick dependency. All cleanup is driven by coroutine completion or
explicit `Stop()` calls.

In the editor and development builds, `BuiltInAudio` also implements the optional `IAudioDiagnosticsSource`
capability (gated behind `#if UNITY_EDITOR || DEVELOPMENT_BUILD`), assembling an `AudioBackendStats` from the
pool and bank counts. The capability is kept off `IAudioService` so the core contract stays minimal and
backends without instrumentation are not forced to implement it.

---

## Diagnostics

`AudioDiagnostics` (in `Systems.Audio`) is a development-only logger shared across the audio assembly.
`Warn` and `Error` are marked `[Conditional("UNITY_EDITOR")]` and `[Conditional("DEVELOPMENT_BUILD")]`, so
both the call and its argument evaluation are stripped from release builds. Messages auto-tag the originating
layer and calling type from `[CallerFilePath]`. The BuiltIn backend routes its authoring and misuse warnings
(redundant preload, undersized pool) through it; genuine runtime failures (a failed Addressables load) stay as
always-on `Debug.LogError`.

`IAudioDiagnosticsSource` and `AudioBackendStats` are likewise fenced behind
`#if UNITY_EDITOR || DEVELOPMENT_BUILD`, so the diagnostics surface, its computation, and the pool tracking
state add nothing to a release build.

---

## Key Decisions

| Decision                                   | Rationale                                                                                                                                                                                                                                                                   |
|--------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Asset GUIDs over address strings           | Stable across renames and moves, validated at author time via `AssetReferenceT<AudioClip>`                                                                                                                                                                                  |
| `UnityEngine.Pool.ObjectPool<AudioSource>` | Built-in, no maintenance, reset and destroy callbacks included                                                                                                                                                                                                              |
| `AudioClip` over `AudioResource`           | No `AudioRandomContainer` needed, all variation owned by calling systems                                                                                                                                                                                                    |
| Coroutine per one-shot handle              | Bounded concurrency, no polling, no tick dependency                                                                                                                                                                                                                         |
| Looping clips always explicit stop         | Caller owns lifecycle, no assumptions about duration                                                                                                                                                                                                                        |
| Cancel and reschedule on pause/resume      | Accurate remaining duration, no global side effects                                                                                                                                                                                                                         |
| Speed changes deferred on pause            | Multiple state changes during pause collapse into one restart on `Resume()`                                                                                                                                                                                                 |
| Three layer volume and speed               | Handle and category layers combine at source write time, neither corrupts the other                                                                                                                                                                                         |
| `ICategoryProvider` over direct reference  | Handle reads category state without access to the full `BuiltInAudio` surface                                                                                                                                                                                               |
| Single `BuiltInAudio` entry point          | Keeps internal types out of the installer, swapping backends requires one setting change                                                                                                                                                                                    |
| Backend owns handle disposal               | `BuiltInAudio` created the handles; it terminates them. `AudioManager` only drops the UUID map.                                                                                                                                                                             |
| `_pendingUnloads` deferred release         | Prevents ghost cache entries when `Unload` races ahead of an in-flight `PreloadAsync` for the same key                                                                                                                                                                      |
| `UniTaskCompletionSource` for `_inFlight`  | A plain `UniTask` can be awaited only once; `.Preserve()` allows sequential re-await but not concurrent awaiters, which is exactly the dedup case. `UniTaskCompletionSource` supports multiple concurrent awaiters, so same-key preloads share one Addressables load safely |
| Zero-minimum clamp on volume and speed     | Prevents `WaitForSecondsRealtime(Infinity)` hangs and unintentional pitch reversal from misconfigured category multipliers                                                                                                                                                  |
| Diagnostics gated by compile symbols       | Dev-only warnings, stats, and pool tracking are stripped from release builds, so no diagnostic state or computation survives production                                                                                                                                     |

---

## Invariants

- `Play` must never be called with a key that was not preloaded. Callers are responsible.
- `AudioSource` components never leave `Runtime/BuiltIn/`. No other layer touches them.
- `AudioClip` never appears in the contract layer. Keys are the only shared identifier.
- Category volume and speed are multipliers applied live. They do not persist across sessions.
- A returned `AudioSource` is always fully reset by the pool's release callback.
- Looping handles must always be explicitly stopped. Failure to do so holds the source until `BuiltInAudio.Dispose`.
- Speed changes on a paused handle are deferred. `Resume()` always recalculates remaining duration.
- Source volume and pitch are always the product of the handle layer and the category layer. Neither layer is ever
  written to source directly.
- The backend is the disposal owner. `AudioManager.Dispose` drops references; `BuiltInAudio.Dispose` terminates handles.
- Category volume and speed multipliers are clamped to zero minimum. The handle `SetVolume` clamp applies the same floor
  at the instance layer.
