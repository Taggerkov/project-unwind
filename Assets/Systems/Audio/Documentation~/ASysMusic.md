# ASys: Music Subsystem

Playlist-based background music management built on top of `AudioManager`.
Game systems activate playlists by type; the subsystem handles loading, sequencing, and auto-advance.

---

## Components

### MusicSettings

A `ScriptableObject` asset with two `AudioEvent[]` arrays: `MenuPlaylist` and `CombatPlaylist`.
Assign it in the Inspector and drag it into `RootInstaller`. All `AudioEvent` entries must have
`AudioCategory.Music`; mismatched entries are skipped with a warning at activation time.

Create via right-click: **Create → Unwind → Audio → Music Settings**.

### PlaylistType

Enum identifying which playlist is active: `None`, `Menu`, `Combat`.

### MusicManager

Plain C# singleton injected by Reflex. Owns playlist activation and the auto-advance policy, and
delegates single-track playback (handle lifecycle, pause/resume/stop, the completion watcher, and the
volume `VolumeFader`) to a composed `AudioStream` scoped to `AudioCategory.Music`. Track sequencing lives
in `PlaylistSequencer`, asset loading in `PlaylistLoader`. Depends on `AudioManager` and `MusicSettings`;
game systems call it directly.

Auto-advance is wired by subscribing to `AudioStream.Completed`, which fires only on natural track end:
the handler advances the sequencer and plays the next track. The watcher cancellation discipline that
prevents a spurious advance lives inside `AudioStream`.

---

## Lifecycle

```
ActivatePlaylist(type)
  → if same type already playing: return immediately (no gap, no reload)
  → cancel any in-flight activation
  → StopAll(Music) + unload current playlist
  → validate tracks (category, key, null)
  → preload in parallel (abort on cancel, skip on load failure)
  → PlayCurrentTrack() → AudioStream.Play()
                                ↓ AudioStream.Completed (natural end only)
                          _sequencer.Advance() → PlayCurrentTrack()
```

`AudioStream` watches completion through `AudioManager.AwaitCompletionAsync`, so there is no polling.
Its watcher token is cancelled before any explicit stop, skip, pause, or replacement (`AwaitCompletionAsync`
fires for both explicit stops and natural completion), so `Completed` is raised only on a genuine track end
and no spurious advance occurs. This discipline lives in `AudioStream`, not `MusicManager`.

---

## Public API

| Method                            | Description                                                                                               |
|-----------------------------------|-----------------------------------------------------------------------------------------------------------|
| `ActivatePlaylist(PlaylistType)`  | Stop current music, unload, preload new playlist, start first track. `PlaylistType.None` silently stops.  |
| `Stop()`                          | Stop playback immediately. Playlist stays loaded; `Resume()` replays current track.                       |
| `Pause()`                         | Pause playback, preserving position. No-op if already paused. Cancels watcher before pausing (see below). |
| `Resume()`                        | Unpause and restart watcher. If no handle is active, starts from current index. No-op if already playing. |
| `NextTrack()`                     | Skip to the next track. Picks randomly if shuffle is on, sequential otherwise.                            |
| `PreviousTrack()`                 | Go back to the previous track. Always sequential regardless of shuffle.                                   |
| `Restart()`                       | Restart the current track from the beginning.                                                             |
| `PlayTrack(AudioEvent)`           | Jump to a specific track by reference. Must exist in the active playlist.                                 |
| `PlayByIndex(int)`                | Jump to a specific track by zero-based index.                                                             |
| `TogglePause()`                   | Pause if playing; resume if paused or stopped. Convenience for pause-menu buttons.                        |
| `SetVolume(float)`                | Set master volume immediately. Cancels any in-flight fade. Clamped to ≥ 0; readable via `Volume`.         |
| `FadeVolumeToAsync(float, float)` | Smoothly interpolate volume to target over a duration. Awaitable. See below.                              |
| `SetSpeed(float)`                 | Set master speed for all music. Wraps `SetCategorySpeed(Music, …)`. Clamped to ≥ 0.                       |
| `SetShuffle(bool)`                | Enable or disable shuffle mode.                                                                           |

**Properties:** `IsPlaying`, `IsPaused`, `CurrentTrack`, `CurrentTrackIndex`, `ActivePlaylist`, `ShuffleEnabled`,
`TrackCount`, `Volume`.

**Events:**

- `OnTrackChanged` — raised on every track change, including auto-advance.
- `OnPlaylistChanged` — raised when the active playlist type changes, including when music stops (`PlaylistType.None`).
  Fires after preloading completes and the first track begins, so `CurrentTrack` and `TrackCount` are valid at
  invocation time.

---

## Pause / Resume Invariant

`AudioSource.isPlaying` returns `false` while paused. If the completion watcher were still running
during a pause, it would see the source as stopped and fire a spurious advance.

The invariant (cancel the watcher before any explicit stop, pause, or replacement, and restart it on
resume) is enforced inside `AudioStream`, so `MusicManager` simply calls `AudioStream.Pause()` and
`AudioStream.Resume()`. Pause and resume act on the single active handle, not the whole category.

---

## Validation

At `ActivatePlaylist` time each track is checked for:

- Null reference → skipped with `LogError`
- Wrong `AudioCategory` (not `Music`) → skipped with `LogWarning`
- Missing Addressables key → skipped with `LogError`
- Looping flag → kept but `LogWarning` (playlist will not auto-advance past this track)

Only validated tracks are preloaded and loaded into the `PlaylistSequencer`. Preloading runs in
parallel (`UniTask.WhenAll` over individual per-track tasks), so a 5-track playlist takes
as long as the single slowest track to load. Playlist order is preserved because
`UniTask.WhenAll(UniTask<T>[])` returns results in input order. Failed tracks are excluded
rather than aborting the entire activation.

---

## Shuffle

When `SetShuffle(true)` is called, `NextTrack` and auto-advance (the `AudioStream.Completed` handler)
both go through `PlaylistSequencer.Advance`, which uses `GetShuffledIndex` instead of incrementing
sequentially.

`GetShuffledIndex` guarantees the same track never plays twice in a row: it draws uniformly
from `[0, length-2]`, then shifts the result up by one if it equals or exceeds the current
index. This maps the reduced range back onto all indices except the current one with uniform
probability. Single-track playlists bypass shuffle silently (no other index exists).

`PreviousTrack` always moves sequentially — tracking shuffle history would require a separate
stack and is intentionally out of scope.

---

## Fade

`FadeVolumeToAsync(float target, float duration)` interpolates `_volume` linearly from its current
value to `target` over `duration` seconds, updating the category volume every frame via
`UniTask.Yield(PlayerLoopTiming.Update)`. The returned `UniTask` completes when the fade finishes.

```csharp
// Fade out, switch playlist, fade back in
await _musicManager.FadeVolumeToAsync(0f, 1f);
_musicManager.ActivatePlaylist(PlaylistType.Combat).Forget();
await _musicManager.FadeVolumeToAsync(1f, 1f);
```

**Cancellation rules:**

- A new `FadeVolumeToAsync` call cancels and replaces any in-flight fade.
- `SetVolume` also cancels any in-flight fade before applying the immediate value.
- `Dispose` cancels and cleans up `_fadeCts`.

If `duration` is zero or negative, the target volume is applied immediately (same as `SetVolume`).
`_volume` is updated continuously during the fade, so `Volume` always reflects the current interpolated
value, not just the start or end points.

---

## Cancellation

Three `CancellationTokenSource` instances govern async operations:

| Field            | Owner          | Cancels                                                                                                                                   |
|------------------|----------------|-------------------------------------------------------------------------------------------------------------------------------------------|
| `_activationCts` | `MusicManager` | The in-flight `ActivatePlaylist` coroutine. Replaced on every new activation call, preventing race conditions on rapid playlist switches. |
| watcher token    | `AudioStream`  | The active completion watcher. Cancelled on any explicit stop, skip, pause, restart, or replacement.                                      |
| `_fadeCts`       | `VolumeFader`  | The active `FadeVolumeToAsync` task. Replaced on every new fade call or immediate `SetVolume`. Cancelled by `Dispose`.                    |

Each is cancelled and disposed via a `CancelAndDispose(ref CancellationTokenSource)` helper that
null-assigns the field after disposal.

---

## Wiring

`MusicManager` is registered in `RootInstaller`:

```csharp
containerBuilder.RegisterType(typeof(MusicManager),
    new[] { typeof(MusicManager), typeof(IDisposable) },
    Lifetime.Singleton, Resolution.Eager);
```

Reflex disposes `MusicManager` before `AudioManager` (reverse registration order), so
`MusicManager.Dispose` can safely call `AudioManager.StopAll`.

Game systems call `ActivatePlaylist` directly:

```csharp
_musicManager.ActivatePlaylist(PlaylistType.Menu).Forget();    // BeginCharacterSelect
_musicManager.ActivatePlaylist(PlaylistType.Combat).Forget();  // BeginCombat
```

---

## Constraints

- All tracks in a playlist must be preloaded before the first `Play`. `ActivatePlaylist` handles this automatically.
- Tracks not in the active playlist are rejected by `PlayTrack` with a `LogWarning`.
- `PlayByIndex` uses the validated, preloaded playlist. Indices map to the post-validation array, not the raw
  `MusicSettings` array.
- A looping track in a playlist prevents auto-advance. Use non-looping tracks or call `NextTrack()` manually.
- Shuffle with a single-track playlist is a no-op; the only track always plays next.
- `SetVolume` and `SetSpeed` apply to the entire `AudioCategory.Music` category and affect any other music played
  outside `MusicManager` if it exists.
- `ActivatePlaylist` returns `UniTask`. Callers use `.Forget()`; unhandled exceptions are reported to
  `UniTaskScheduler.UnobservedTaskException`.
