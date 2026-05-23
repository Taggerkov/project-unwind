using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.Audio.Contracts;
using Systems.Audio.Runtime.BuiltIn;
using Systems.Audio.Shared;
using UnityEngine;

namespace Systems.Audio
{
    /// <summary>
    /// Authoritative surface for all audio operations.
    /// Owns UUID-to-playback tracking and delegates to the active backend.
    /// </summary>
    public sealed class AudioManager : IDisposable
    {
        /// <summary>Maps active UUIDs to their underlying playback handles.</summary>
        private readonly Dictionary<Guid, IAudioHandle> _handles = new();

        /// <summary>Maps active UUIDs to the name of the AudioEvent that triggered them.</summary>
        private readonly Dictionary<Guid, string> _handleNames = new();

        /// <summary>The active audio playback backend.</summary>
        private readonly IAudioService _service;

        /// <summary>
        /// Constructs the manager and initialises the backend specified in <paramref name="settings"/>.
        /// </summary>
        /// <param name="settings">Audio system configuration asset.</param>
        public AudioManager(Shared.AudioSettings settings)
        {
            _service = settings.Backend switch
            {
                AudioBackend.BuiltIn => new BuiltInAudio(settings),
                AudioBackend.FMOD => throw new NotImplementedException("[AudioManager] FMOD backend is not yet implemented."),
                _ => throw new ArgumentOutOfRangeException(nameof(settings.Backend), "[AudioManager] Unknown audio backend.")
            };
        }

        // ── Preload / Unload ────────────────────────────────────────────────

        /// <summary>
        /// Preloads the clip associated with the given <see cref="AudioEvent"/>.
        /// Must complete before calling <see cref="Play"/>.
        /// </summary>
        /// <param name="audioEvent">The sound event to preload.</param>
        /// <param name="cancellationToken">Token to cancel the load operation.</param>
        public async UniTask PreloadAsync(AudioEvent audioEvent, CancellationToken cancellationToken = default)
        {
            if (audioEvent == null)
            {
                AudioDiagnostics.Warn("PreloadAsync called with null AudioEvent.");
                return;
            }

            await _service.PreloadAsync(audioEvent.Key, cancellationToken);
        }

        /// <summary>
        /// Preloads all clips in <paramref name="audioEvents"/> in parallel.
        /// Null entries are skipped with a warning. Must complete before calling <see cref="Play"/>
        /// on any event in the collection.
        /// </summary>
        /// <param name="audioEvents">The sound events to preload.</param>
        /// <param name="cancellationToken">Token to cancel all pending load operations.</param>
        public async UniTask PreloadAsync(IEnumerable<AudioEvent> audioEvents, CancellationToken cancellationToken = default)
        {
            if (audioEvents == null)
            {
                AudioDiagnostics.Warn("PreloadAsync called with null collection.");
                return;
            }

            var tasks = new List<UniTask>();
            foreach (var audioEvent in audioEvents)
            {
                if (audioEvent == null)
                {
                    AudioDiagnostics.Warn("Null AudioEvent in PreloadAsync collection. Skipping.");
                    continue;
                }
                tasks.Add(_service.PreloadAsync(audioEvent.Key, cancellationToken));
            }

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks);
        }

        /// <summary>
        /// Releases the clip associated with the given <see cref="AudioEvent"/> from memory.
        /// Active sounds playing that clip continue until they stop naturally.
        /// </summary>
        /// <param name="audioEvent">The sound event to unload.</param>
        /// <returns>True if <paramref name="audioEvent"/> was non-null. Does not indicate whether the clip was in memory.</returns>
        public bool Unload(AudioEvent audioEvent)
        {
            if (audioEvent == null)
            {
                AudioDiagnostics.Warn("Unload called with null AudioEvent.");
                return false;
            }

            _service.Unload(audioEvent.Key);
            return true;
        }

        // ── Playback ────────────────────────────────────────────────────────

        /// <summary>
        /// Plays the given <see cref="AudioEvent"/> using its default values.
        /// </summary>
        /// <param name="audioEvent">The sound event to play.</param>
        /// <returns>
        /// A <see cref="Guid"/> identifying this playback instance.
        /// Returns <see cref="Guid.Empty"/> if <paramref name="audioEvent"/> is null or the clip was
        /// not preloaded.
        /// </returns>
        /// <remarks>
        /// The returned <see cref="Guid"/> reflects the UUID confirmed by the backend.
        /// If the <see cref="AudioEvent"/> is configured to loop, the caller must retain
        /// this UUID and call <see cref="Stop"/> explicitly. Looping sounds are never
        /// stopped automatically.
        /// </remarks>
        public Guid Play(AudioEvent audioEvent)
        {
            if (audioEvent == null)
            {
                AudioDiagnostics.Warn("Play called with null AudioEvent.");
                return Guid.Empty;
            }

            IAudioHandle handle;
            try
            {
                var request = AudioRequest.FromAudioEvent(audioEvent);
                var uuid = Guid.NewGuid();
                handle = _service.Play(in request, uuid);
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogError($"[AudioManager] {ex.Message}");
                return Guid.Empty;
            }

            var id = handle.Uuid;
            handle.OnReleased += OnHandleReleased;
            _handles[id] = handle;
            _handleNames[id] = audioEvent.name;
            return id;
        }

        /// <summary>Stops the playback identified by <paramref name="uuid"/>.</summary>
        /// <param name="uuid"><see cref="Guid"/> returned by <see cref="Play"/>.</param>
        /// <returns>True if the playback was found and stopped.</returns>
        public bool Stop(Guid uuid)
        {
            if (!_handles.TryGetValue(uuid, out var handle)) return false;
            handle.Stop();
            return true;
        }

        /// <summary>Pauses the playback identified by <paramref name="uuid"/>.</summary>
        /// <param name="uuid"><see cref="Guid"/> returned by <see cref="Play"/>.</param>
        /// <returns>True if the playback was found and paused.</returns>
        public bool Pause(Guid uuid) => Apply(uuid, h => h.Pause());

        /// <summary>Resumes the playback identified by <paramref name="uuid"/>.</summary>
        /// <param name="uuid"><see cref="Guid"/> returned by <see cref="Play"/>.</param>
        /// <returns>True if the playback was found and resumed.</returns>
        public bool Resume(Guid uuid) => Apply(uuid, h => h.Resume());

        /// <summary>Sets the volume on the playback identified by <paramref name="uuid"/>.</summary>
        /// <param name="uuid"><see cref="Guid"/> returned by <see cref="Play"/>.</param>
        /// <param name="volume">Target volume. Must be zero or greater.</param>
        /// <returns>True if the playback was found and the volume was applied.</returns>
        public bool SetVolume(Guid uuid, float volume) => Apply(uuid, h => h.SetVolume(volume));

        /// <summary>Sets the speed on the playback identified by <paramref name="uuid"/>.</summary>
        /// <param name="uuid"><see cref="Guid"/> returned by <see cref="Play"/>.</param>
        /// <param name="speed">Target speed multiplier.</param>
        /// <returns>True if the playback was found and speed was applied.</returns>
        public bool SetSpeed(Guid uuid, float speed) => Apply(uuid, h => h.SetSpeed(speed));

        /// <summary>
        /// Returns the volume of the playback identified by <paramref name="uuid"/>, or
        /// <paramref name="fallback"/> if the playback is not active. Excludes the category multiplier.
        /// </summary>
        /// <param name="uuid"><see cref="Guid"/> returned by <see cref="Play"/>.</param>
        /// <param name="fallback">Value returned when the playback is not found. Defaults to 1.</param>
        public float GetVolume(Guid uuid, float fallback = 1f) =>
            _handles.TryGetValue(uuid, out var handle) ? handle.Volume : fallback;

        /// <summary>
        /// Captures a read-only <see cref="AudioPlaybackSnapshot"/> of the playback identified by <paramref name="uuid"/>.
        /// </summary>
        /// <param name="uuid"><see cref="Guid"/> returned by <see cref="Play"/>.</param>
        /// <param name="info">The populated snapshot when the playback is active; default otherwise.</param>
        /// <returns>True if an active playback with that UUID exists.</returns>
        public bool TryGetSnapshot(Guid uuid, out AudioPlaybackSnapshot info)
        {
            if (!_handles.TryGetValue(uuid, out var handle))
            {
                info = default;
                return false;
            }

            info = new AudioPlaybackSnapshot(uuid, GetClipName(uuid), handle.Category, handle.IsPlaying,
                handle.IsPaused, handle.IsLooping, handle.Volume, handle.Speed, handle.Time, handle.Length);
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Captures backend statistics for diagnostics tooling when the active backend supports it.
        /// Available only in the editor and development builds.
        /// </summary>
        /// <param name="stats">The populated stats when supported; default otherwise.</param>
        /// <returns>True if the active backend reports statistics.</returns>
        public bool TryGetBackendStats(out AudioBackendStats stats)
        {
            if (_service is IAudioDiagnosticsSource source)
                return source.TryGetStats(out stats);

            stats = default;
            return false;
        }
#endif

        /// <summary>Returns true if the playback identified by <paramref name="uuid"/> is active.</summary>
        /// <param name="uuid"><see cref="Guid"/> returned by <see cref="Play"/>.</param>
        public bool IsPlaying(Guid uuid) => _handles.TryGetValue(uuid, out var handle) && handle.IsPlaying;

        /// <summary>Returns true if the playback identified by <paramref name="uuid"/> is currently paused.</summary>
        /// <param name="uuid"><see cref="Guid"/> returned by <see cref="Play"/>.</param>
        public bool IsPaused(Guid uuid) => _handles.TryGetValue(uuid, out var handle) && handle.IsPaused;

        /// <summary>
        /// Returns a <see cref="UniTask"/> that completes when the playback identified by <paramref name="uuid"/> ends,
        /// either naturally or via <see cref="Stop"/>. Completes immediately if the handle is no longer active.
        /// </summary>
        /// <remarks>
        /// Fires for both explicit stops and natural completion. Callers that only want to react to
        /// natural completion must cancel <paramref name="ct"/> before calling <see cref="Stop"/> on the
        /// same handle; <see cref="UniTaskExtensions.SuppressCancellationThrow"/> then distinguishes the two cases.
        /// </remarks>
        /// <param name="uuid"><see cref="Guid"/> returned by <see cref="Play"/>.</param>
        /// <param name="ct">Token to abandon the wait without advancing.</param>
        public UniTask AwaitCompletionAsync(Guid uuid, CancellationToken ct = default)
        {
            if (!_handles.TryGetValue(uuid, out var handle))
                return UniTask.CompletedTask;

            var tcs = new UniTaskCompletionSource();
            CancellationTokenRegistration registration = default;
            
            handle.OnReleased += OnReleased;
            registration = ct.Register(() =>
            {
                handle.OnReleased -= OnReleased;
                tcs.TrySetCanceled();
            });

            return tcs.Task;
            
            void OnReleased(Guid _)
            {
                handle.OnReleased -= OnReleased;
                registration.Dispose();
                tcs.TrySetResult();
            }
        }

        /// <summary>The UUIDs of all currently active playback instances.</summary>
        public IEnumerable<Guid> ActiveUuids => _handles.Keys;

        // ── Category control ────────────────────────────────────────────────

        /// <summary>Stops all active sounds in the given category.</summary>
        public void StopAll(AudioCategory category) => _service.StopAll(category);

        /// <summary>Pauses all active sounds in the given category.</summary>
        public void PauseAll(AudioCategory category) => _service.PauseAll(category);

        /// <summary>Resumes all paused sounds in the given category.</summary>
        public void ResumeAll(AudioCategory category) => _service.ResumeAll(category);

        /// <summary>
        /// Sets the master volume for the given category.
        /// Must be zero or greater.
        /// </summary>
        /// <param name="category">Target category.</param>
        /// <param name="volume">Target volume. Must be zero or greater.</param>
        public void SetCategoryVolume(AudioCategory category, float volume) =>
            _service.SetCategoryVolume(category, volume);

        /// <summary>
        /// Sets the master speed for the given category.
        /// </summary>
        /// <param name="category">Target category.</param>
        /// <param name="speed">Target speed multiplier.</param>
        public void SetCategorySpeed(AudioCategory category, float speed) =>
            _service.SetCategorySpeed(category, speed);

        /// <summary>Returns the current master volume for the given category.</summary>
        /// <param name="category">Target category.</param>
        public float GetCategoryVolume(AudioCategory category) => _service.GetCategoryVolume(category);

        /// <summary>Returns the current master speed for the given category.</summary>
        /// <param name="category">Target category.</param>
        public float GetCategorySpeed(AudioCategory category) => _service.GetCategorySpeed(category);

        // ── Disposal ────────────────────────────────────────────────────────

        /// <summary>
        /// Unsubscribes all release listeners and drops playback references, then delegates
        /// resource cleanup to the backend. Active handles are stopped by the backend during
        /// its own <see cref="IDisposable.Dispose"/> call.
        /// </summary>
        public void Dispose()
        {
            foreach (var handle in _handles.Values)
                handle.OnReleased -= OnHandleReleased;

            _handles.Clear();
            _service.Dispose();
        }

        // ── Internal ────────────────────────────────────────────────────────

        /// <summary>
        /// Removes the handle associated with <paramref name="uuid"/> from <see cref="_handles"/>.
        /// Subscribed to <see cref="IAudioHandle.OnReleased"/> at playtime.
        /// </summary>
        private void OnHandleReleased(Guid uuid)
        {
            _handles.Remove(uuid);
            _handleNames.Remove(uuid);
        }

        /// <summary>Returns the AudioEvent name for the playback identified by <paramref name="uuid"/>, or a short UUID fallback.</summary>
        public string GetClipName(Guid uuid) =>
            _handleNames.TryGetValue(uuid, out var name) ? name : uuid.ToString()[..8];

        /// <summary>
        /// Retrieves the handle associated with <paramref name="uuid"/> and invokes <paramref name="action"/> on it.
        /// </summary>
        /// <param name="uuid"><see cref="Guid"/> returned by <see cref="Play"/>.</param>
        /// <param name="action">The operation to perform on the handle.</param>
        /// <returns>True if the handle was found and the action was invoked.</returns>
        private bool Apply(Guid uuid, Action<IAudioHandle> action)
        {
            if (!_handles.TryGetValue(uuid, out var handle)) return false;
            action(handle);
            return true;
        }
    }
}
