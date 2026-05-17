using System;
using System.Collections.Generic;
using System.Threading;
using Systems.Audio.Contracts;
using Systems.Audio.Runtime.BuiltIn;
using UnityEngine;

namespace Systems.Audio
{
    /// <summary>
    /// The available audio backends. Determines which implementation of <see cref="IAudioService"/> is constructed at runtime.
    /// </summary>
    public enum AudioBackend
    {
        /// <summary>Unity <see cref="AudioSource"/> implementation.</summary>
        BuiltIn,

        /// <summary>FMOD Studio implementation. Requires the FMOD Studio Unity package.</summary>
        FMOD
    }

    /// <summary>
    /// Authoritative surface for all audio operations.
    /// Owns UUID-to-playback tracking and delegates to the active backend.
    /// </summary>
    public sealed class AudioManager : IDisposable
    {
        /// <summary>Maps active UUIDs to their underlying playback handles.</summary>
        private readonly Dictionary<Guid, IAudioHandle> _handles = new();

        /// <summary>The active audio playback backend.</summary>
        private readonly IAudioService _service;

        /// <summary>
        /// Constructs the manager and initialises the backend specified in <paramref name="settings"/>.
        /// </summary>
        /// <param name="settings">Audio system configuration asset.</param>
        public AudioManager(AudioSettings settings)
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
        public async Awaitable PreloadAsync(AudioEvent audioEvent, CancellationToken cancellationToken = default)
        {
            if (audioEvent == null)
            {
                Debug.LogWarning("[AudioManager] PreloadAsync called with null AudioEvent.");
                return;
            }

            await _service.PreloadAsync(audioEvent.Key, cancellationToken);
        }

        /// <summary>
        /// Releases the clip associated with the given <see cref="AudioEvent"/> from memory.
        /// Active sounds playing that clip continue until they stop naturally.
        /// </summary>
        /// <param name="audioEvent">The sound event to unload.</param>
        /// <returns>True if the sound event was valid and unloaded.</returns>
        public bool Unload(AudioEvent audioEvent)
        {
            if (audioEvent == null)
            {
                Debug.LogWarning("[AudioManager] Unload called with null AudioEvent.");
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
        /// Returns <see cref="Guid.Empty"/> if <paramref name="audioEvent"/> is null.
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
                Debug.LogWarning("[AudioManager] Play called with null AudioEvent.");
                return Guid.Empty;
            }

            var request = AudioRequest.FromAudioEvent(audioEvent);
            var uuid = Guid.NewGuid();
            var handle = _service.Play(in request, uuid);

            if (uuid != handle.Uuid)
                Debug.LogWarning(
                    "[AudioManager] Backend returned a different UUID than the one assigned. Using backend value.");

            uuid = handle.Uuid;
            handle.OnReleased += OnHandleReleased;
            _handles[uuid] = handle;
            return uuid;
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

        /// <summary>Returns true if the playback identified by <paramref name="uuid"/> is active.</summary>
        /// <param name="uuid"><see cref="Guid"/> returned by <see cref="Play"/>.</param>
        public bool IsPlaying(Guid uuid) => _handles.TryGetValue(uuid, out var handle) && handle.IsPlaying;

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

        // ── Disposal ────────────────────────────────────────────────────────

        /// <summary>
        /// Ensures all active playbacks are cleanly terminated and resources are released
        /// when the manager is disposed of.
        /// </summary>
        public void Dispose()
        {
            foreach (var handle in _handles.Values)
            {
                handle.OnReleased -= OnHandleReleased;
                handle.Stop();
            }

            _handles.Clear();
        }

        // ── Internal ────────────────────────────────────────────────────────

        /// <summary>
        /// Removes the handle associated with <paramref name="uuid"/> from <see cref="_handles"/>.
        /// Subscribed to <see cref="IAudioHandle.OnReleased"/> at playtime.
        /// </summary>
        private void OnHandleReleased(Guid uuid) => _handles.Remove(uuid);

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