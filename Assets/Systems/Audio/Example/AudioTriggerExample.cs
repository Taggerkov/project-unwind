using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Systems.Audio.Example
{
    /// <summary>
    /// Example of how to trigger a single sound from a plain C# system.
    /// Preloads the clip at construction. If <see cref="Play"/> is called before preload completes,
    /// playback waits for it internally. Not intended for production use.
    /// </summary>
    /// <example>
    /// Construct with an <see cref="AudioEvent"/> and call <see cref="Play"/> from any system:
    /// <code>
    /// // Declaration
    /// private readonly AudioTriggerExample _trigger;
    /// 
    /// // Initialisation (Use in constructor or any pre-call env to avoid PreLoad() race conditions!)
    /// _trigger = new AudioTriggerExample(audioManager, audioEvent);
    /// 
    /// // Trigger
    /// private void OnHit() => _trigger.Play();
    /// </code>
    /// </example>
    public sealed class AudioTriggerExample : IDisposable
    {
        /// <summary>The authoritative audio surface.</summary>
        private readonly AudioManager _audioManager;

        /// <summary>The sound to play.</summary>
        private readonly AudioEvent _audioEvent;

        /// <summary>Whether the clip has been preloaded.</summary>
        private bool _preloaded;

        /// <summary>Cancellation token source tied to this instance's lifetime.</summary>
        private readonly CancellationTokenSource _cts;

        /// <summary>
        /// Wraps the given <see cref="AudioEvent"/> for single-sound playback.
        /// </summary>
        /// <param name="audioManager">The authoritative audio surface.</param>
        /// <param name="audioEvent">The sound to play.</param>
        public AudioTriggerExample(AudioManager audioManager, AudioEvent audioEvent)
        {
            _audioManager = audioManager;
            _audioEvent = audioEvent;
            _cts = new CancellationTokenSource();
            PreloadAsync().Forget();
        }

        /// <summary>
        /// Plays the sound.
        /// Preload is initiated at construction and should be complete by the time this is called.
        /// If called before preload completes, playback waits internally until ready.
        /// </summary>
        public void Play() => PlayAsync();

        /// <summary>
        /// Cancels any in-flight preload operation tied to this instance's lifetime.
        /// </summary>
        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        /// <summary>
        /// Awaits preload completion if still in progress, then plays.
        /// Separated from <see cref="Play"/> to contain the async concern internally.
        /// </summary>
        private async UniTaskVoid PlayAsync()
        {
            Debug.LogWarning("[AudioTriggerExample] Sound Fired!.");
            await PreloadAsync();
            _audioManager.Play(_audioEvent);
        }
        
        /// <summary>
        /// Loads and caches the clip. Returns immediately if already loaded.
        /// </summary>
        private async UniTask PreloadAsync()
        {
            if (_preloaded) return;
            await _audioManager.PreloadAsync(_audioEvent, _cts.Token);
            _preloaded = true;
        }
    }
}