# Audio System, BuiltIn Implementation

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

Unloading a clip releases the Addressables handle and removes it from the cache. Any
`AudioSource` already playing that clip holds its own reference and continues until natural
completion.

### UnityAudioPool

Wraps `UnityEngine.Pool.ObjectPool<AudioSource>`. Creates a dedicated `GameObject` marked
`DontDestroyOnLoad` to host pooled sources and run coroutines. Sources are fully reset on
return via the pool's release callback. The pool also serves as the coroutine host for
one-shot cleanup, forwarding `StartCoroutine` and `StopCoroutine` to its host `MonoBehaviour`.

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

`SetVolume` and `SetSpeed` update the handle layer and trigger an immediate recalculation.
Category changes are triggered by `BuiltInAudio`, which calls `ApplyVolume` and `ApplySpeed`
on each affected handle after updating its own category dictionaries.

One-shot clips schedule their own cleanup via a coroutine that calculates remaining duration
from current speed and playback position. The coroutine is cancelled and rescheduled whenever
speed changes while playing, and always rescheduled on resume to account for any speed changes
accumulated during pause.

Looping clips have no coroutine. Cleanup is always explicit via `Stop()`. A looping handle
left without a `Stop()` call holds its rented `AudioSource` indefinitely.

On release, whether via `Stop()` or natural coroutine completion, the handle raises `OnReleased`,
returns the source to the pool, and nulls its internal reference.

### BuiltInAudio

Single entry point for the BuiltIn backend. Owns and orchestrates `UnityAudioBank` and
`UnityAudioPool`, constructing them internally from `AudioSettings`. Registered as `IAudioService`
and `ICategoryProvider` in the container. Callers never interact with internal types directly.

Tracks active handles per category in a `Dictionary<AudioCategory, List<AudioHandle>>` for
bulk operations. Category volume and speed multipliers are stored separately and queried live
by handles via `ICategoryProvider`. On category change, `BuiltInAudio` iterates only the
affected category and triggers recalculation on each handle.

`Play` requires the requested key to be preloaded. If `TryGet` returns false, an
`InvalidOperationException` is thrown. The caller is responsible for preloading before play.

There is no polling and no tick dependency. All cleanup is driven by coroutine completion or
explicit `Stop()` calls.

---

## Key Decisions

| Decision | Rationale |
|---|---|
| Asset GUIDs over address strings | Stable across renames and moves, validated at author time via `AssetReferenceT<AudioClip>` |
| `UnityEngine.Pool.ObjectPool<AudioSource>` | Built-in, no maintenance, reset and destroy callbacks included |
| `AudioClip` over `AudioResource` | No `AudioRandomContainer` needed, all variation owned by calling systems |
| Coroutine per one-shot handle | Bounded concurrency, no polling, no tick dependency |
| Looping clips always explicit stop | Caller owns lifecycle, no assumptions about duration |
| Cancel and reschedule on pause/resume | Accurate remaining duration, no global side effects |
| Speed changes deferred on pause | Multiple state changes during pause collapse into one restart on `Resume()` |
| Three layer volume and speed | Handle and category layers combine at source write time, neither corrupts the other |
| `ICategoryProvider` over direct reference | Handle reads category state without access to the full `BuiltInAudio` surface |
| Single `BuiltInAudio` entry point | Keeps internal types out of the installer, swapping backends requires one setting change |

---

## Invariants

- `Play` must never be called with a key that was not preloaded. Callers are responsible.
- `AudioSource` components never leave `Runtime/BuiltIn/`. No other layer touches them.
- `AudioClip` never appears in the contract layer. Keys are the only shared identifier.
- Category volume and speed are multipliers applied live. They do not persist across sessions.
- A returned `AudioSource` is always fully reset by the pool's release callback.
- Looping handles must always be explicitly stopped. Failure to do so leaks the rented `AudioSource` indefinitely.
- Speed changes on a paused handle are deferred. `Resume()` always recalculates remaining duration.
- Source volume and pitch are always the product of the handle layer and the category layer. Neither layer is ever written to source directly.