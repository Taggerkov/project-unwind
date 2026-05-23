using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.Audio.Contracts;
using Systems.Audio.Shared;
using TMPro;

namespace Systems.Audio.Voiceline
{
    /// <summary>
    /// Manages character voiceline playback with priority-based queuing, interruption, subtitle display,
    /// localization, pause/resume, and runtime volume control.
    /// Pure C# singleton injected via Reflex. Delegates single-track playback lifecycle to <see cref="AudioStream"/>
    /// and layers the queue, priority, and subtitle policy on top.
    /// Callers must preload VoicelineEvents before calling Play.
    /// </summary>
    public sealed class VoicelineManager : IDisposable
    {
        // ── Dependencies ────────────────────────────────────────────────────

        /// <summary>The audio playback surface used for preload and unload operations.</summary>
        private readonly AudioManager _audioManager;

        /// <summary>The language system tracking the current runtime language for subtitle localisation.</summary>
        private readonly LanguageSystem _languageSystem;

        /// <summary>The single-track playback engine, scoped to <see cref="AudioCategory.Voice"/>.</summary>
        private readonly AudioStream _stream;

        // ── Internal State ──────────────────────────────────────────────────

        /// <summary>Priority queue maintaining pending voicelines ordered by priority, FIFO within the same tier.</summary>
        private readonly PriorityQueue<QueuedVoiceline> _queue;

        /// <summary>The UGUI component for subtitle rendering. Null until bound via <see cref="SetSubtitleDisplay"/>.</summary>
        private TextMeshProUGUI _subtitleDisplay;

        /// <summary>The currently playing voiceline event. Null when idle.</summary>
        private VoicelineEvent _currentVoiceline;

        /// <summary>The priority of the currently playing voiceline. Null when idle.</summary>
        private VoicelinePriority? _currentPriority;

        // ── Public Properties ───────────────────────────────────────────────

        /// <summary>Returns the currently playing VoicelineEvent, or null if idle.</summary>
        public VoicelineEvent CurrentVoiceline => _currentVoiceline;

        /// <summary>Returns the priority of the currently playing voiceline, or null if idle.</summary>
        public VoicelinePriority? CurrentPriority => _currentPriority;

        /// <summary>Returns true if a voiceline is actively playing (not paused).</summary>
        public bool IsPlaying => _stream.IsPlaying;

        /// <summary>Returns true if playback is currently paused.</summary>
        public bool IsPaused => _stream.IsPaused;

        /// <summary>Returns the number of voicelines in the queue.</summary>
        public int QueueCount => _queue.Count;

        /// <summary>Returns true when no voiceline is playing, paused, or queued.</summary>
        public bool IsIdle => !_stream.IsPlaying && !_stream.IsPaused && _queue.Count == 0;

        /// <summary>Returns the current master volume for all voice playback.</summary>
        public float Volume => _stream.Volume;

        // ── Public Events ───────────────────────────────────────────────────

        /// <summary>Raised when a voiceline begins playing.</summary>
        public event Action<VoicelineEvent> OnVoicelineStarted;

        /// <summary>Raised when a voiceline completes naturally.</summary>
        public event Action<VoicelineEvent> OnVoicelineCompleted;

        /// <summary>Raised when a voiceline is stopped by a higher-priority voiceline.</summary>
        public event Action<VoicelineEvent> OnVoicelineInterrupted;

        /// <summary>Raised when a voiceline cannot be played because its clip was not preloaded.</summary>
        public event Action<VoicelineEvent> OnVoicelineFailed;

        /// <summary>Raised when the last queued voiceline completes and no more are queued.</summary>
        public event Action OnQueueEmpty;

        // ── Constructor ─────────────────────────────────────────────────────

        /// <summary>Constructs the manager. Called by Reflex via constructor injection.</summary>
        /// <param name="audioManager">The audio playback surface.</param>
        /// <param name="languageSystem">Provides the current runtime language for subtitle localisation.</param>
        public VoicelineManager(AudioManager audioManager, LanguageSystem languageSystem)
        {
            _audioManager = audioManager;
            _languageSystem = languageSystem;
            _queue = new PriorityQueue<QueuedVoiceline>();
            _stream = new AudioStream(audioManager, AudioCategory.Voice);
            _stream.Completed += OnPlaybackCompleted;
        }

        // ── UI Wiring ───────────────────────────────────────────────────────

        /// <summary>
        /// Binds the UGUI component that displays subtitle text. Call once from a scene-level MonoBehaviour.
        /// Passing null silently disables subtitle rendering.
        /// </summary>
        /// <param name="display">The TextMeshProUGUI component to write subtitles to, or null to disable.</param>
        public void SetSubtitleDisplay(TextMeshProUGUI display) => _subtitleDisplay = display;

        // ── Preload / Unload ────────────────────────────────────────────────

        /// <summary>
        /// Preloads the audio clip for <paramref name="voicelineEvent"/>. Must complete before calling Play.
        /// </summary>
        /// <param name="voicelineEvent">The voiceline event to preload.</param>
        /// <param name="ct">Token to cancel the load operation.</param>
        public async UniTask PreloadAsync(VoicelineEvent voicelineEvent, CancellationToken ct = default)
        {
            if (voicelineEvent?.AudioEvent == null)
            {
                AudioDiagnostics.Warn("PreloadAsync called with null VoicelineEvent or AudioEvent.");
                return;
            }

            await _audioManager.PreloadAsync(voicelineEvent.AudioEvent, ct);
        }

        /// <summary>
        /// Preloads all clips in <paramref name="voicelineEvents"/> in parallel. Null entries are skipped.
        /// </summary>
        /// <param name="voicelineEvents">The collection of voiceline events to preload.</param>
        /// <param name="ct">Token to cancel all pending load operations.</param>
        public async UniTask PreloadAsync(IEnumerable<VoicelineEvent> voicelineEvents, CancellationToken ct = default)
        {
            if (voicelineEvents == null)
            {
                AudioDiagnostics.Warn("PreloadAsync called with null collection.");
                return;
            }

            var tasks = new List<UniTask>();
            foreach (var vle in voicelineEvents)
            {
                if (vle?.AudioEvent == null) continue;
                tasks.Add(_audioManager.PreloadAsync(vle.AudioEvent, ct));
            }

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks);
        }

        /// <summary>
        /// Releases the clip for <paramref name="voicelineEvent"/> from memory.
        /// Call this with the same events passed to <see cref="PreloadAsync(VoicelineEvent, CancellationToken)"/> when they are no longer needed.
        /// </summary>
        /// <param name="voicelineEvent">The voiceline event whose clip to release.</param>
        public void Unload(VoicelineEvent voicelineEvent)
        {
            if (voicelineEvent?.AudioEvent == null)
            {
                AudioDiagnostics.Warn("Unload called with null VoicelineEvent or AudioEvent.");
                return;
            }

            _audioManager.Unload(voicelineEvent.AudioEvent);
        }

        /// <summary>
        /// Releases clips for all events in <paramref name="voicelineEvents"/> from memory. Null entries are skipped.
        /// </summary>
        /// <param name="voicelineEvents">The collection of voiceline events whose clips to release.</param>
        public void Unload(IEnumerable<VoicelineEvent> voicelineEvents)
        {
            if (voicelineEvents == null)
            {
                AudioDiagnostics.Warn("Unload called with null collection.");
                return;
            }

            foreach (var vle in voicelineEvents)
            {
                if (vle?.AudioEvent == null) continue;
                _audioManager.Unload(vle.AudioEvent);
            }
        }

        // ── Playback Control ────────────────────────────────────────────────

        /// <summary>Requests voiceline playback using the <see cref="VoicelineEvent.DefaultPriority"/> defined on the asset.</summary>
        /// <param name="voicelineEvent">The voiceline event to play.</param>
        public void Play(VoicelineEvent voicelineEvent) =>
            Play(voicelineEvent, voicelineEvent?.DefaultPriority ?? VoicelinePriority.Normal);

        /// <summary>
        /// Requests voiceline playback at the given priority.
        /// Plays immediately if idle. Interrupts the current voiceline if the new priority is strictly higher.
        /// Otherwise enqueues behind all voicelines with equal or higher priority.
        /// </summary>
        /// <param name="voicelineEvent">The voiceline event to play.</param>
        /// <param name="priority">The priority to assign to this request.</param>
        public void Play(VoicelineEvent voicelineEvent, VoicelinePriority priority)
        {
            if (voicelineEvent?.AudioEvent == null)
            {
                AudioDiagnostics.Warn("Play called with null VoicelineEvent or AudioEvent.");
                return;
            }

            if (!_stream.IsPlaying && !_stream.IsPaused)
            {
                PlayImmediate(voicelineEvent, priority);
                return;
            }

            if (ShouldInterrupt(priority))
            {
                var interrupted = _currentVoiceline;
                StopCurrent();
                OnVoicelineInterrupted?.Invoke(interrupted);
                PlayImmediate(voicelineEvent, priority);
            }
            else
            {
                _queue.Enqueue(new QueuedVoiceline(voicelineEvent, priority), (int)priority);
            }
        }

        /// <summary>
        /// Stops current playback and clears the entire queue.
        /// </summary>
        public void Stop()
        {
            StopCurrent();
            _queue.Clear();
        }

        /// <summary>
        /// Stops current playback and immediately begins the next queued voiceline, if any.
        /// Also works when paused. Has no effect if idle.
        /// </summary>
        public void Skip()
        {
            if (!_stream.IsPlaying && !_stream.IsPaused) return;
            StopCurrent();
            PlayNextQueued();
        }

        /// <summary>
        /// Stops the current voiceline and replays it from the beginning at the same priority.
        /// Has no effect if idle.
        /// </summary>
        public void Restart()
        {
            if (!_stream.IsPlaying && !_stream.IsPaused) return;
            var voiceline = _currentVoiceline;
            var priority = _currentPriority ?? VoicelinePriority.Normal;
            StopCurrent();
            PlayImmediate(voiceline, priority);
        }

        /// <summary>
        /// Clears all queued voicelines without stopping the currently playing one.
        /// </summary>
        public void Clear() => _queue.Clear();

        /// <summary>
        /// Pauses the currently playing voiceline, preserving its position.
        /// Has no effect if not currently playing.
        /// </summary>
        public void Pause() => _stream.Pause();

        /// <summary>
        /// Resumes a paused voiceline from the position at which it was paused.
        /// Has no effect if not currently paused.
        /// </summary>
        public void Resume() => _stream.Resume();

        /// <summary>Pauses if currently playing; resumes if currently paused. Has no effect if idle.</summary>
        public void TogglePause()
        {
            if (_stream.IsPlaying) _stream.Pause();
            else if (_stream.IsPaused) _stream.Resume();
        }

        // ── Volume ──────────────────────────────────────────────────────────

        /// <summary>Sets the master volume for all voice playback immediately. Cancels any in-flight fade.</summary>
        /// <param name="volume">Target volume. Clamped to a zero minimum.</param>
        public void SetVolume(float volume) => _stream.SetVolume(volume);

        /// <summary>Smoothly interpolates the voice volume to <paramref name="target"/> over <paramref name="duration"/> seconds.</summary>
        /// <param name="target">Target volume. Clamped to a zero minimum.</param>
        /// <param name="duration">Fade duration in seconds. Zero or negative applies immediately.</param>
        public UniTask FadeVolumeToAsync(float target, float duration) => _stream.FadeVolumeToAsync(target, duration);

        // ── State Inspection ────────────────────────────────────────────────

        /// <summary>
        /// Returns a read-only snapshot of queued voicelines in priority order.
        /// </summary>
        /// <returns>A list of queued events ordered from highest to lowest priority.</returns>
        public IReadOnlyList<VoicelineEvent> GetQueuedVoicelines()
        {
            var items = _queue.GetAllItems();
            var result = new List<VoicelineEvent>(items.Count);
            foreach (var queued in items)
                result.Add(queued.VoicelineEvent);
            return result;
        }

        /// <summary>
        /// Removes the first occurrence of <paramref name="voicelineEvent"/> from the queue.
        /// Has no effect on the currently playing voiceline.
        /// </summary>
        /// <param name="voicelineEvent">The event to remove.</param>
        /// <returns>True if the event was found and removed.</returns>
        public bool RemoveFromQueue(VoicelineEvent voicelineEvent)
        {
            if (voicelineEvent == null) return false;
            return _queue.RemoveFirst(qv => qv.VoicelineEvent == voicelineEvent);
        }

        /// <summary>Returns true if <paramref name="voicelineEvent"/> is present in the queue.</summary>
        /// <param name="voicelineEvent">The event to check for.</param>
        public bool IsQueued(VoicelineEvent voicelineEvent)
        {
            if (voicelineEvent == null) return false;
            return _queue.Contains(qv => qv.VoicelineEvent == voicelineEvent);
        }

        // ── Lifecycle ───────────────────────────────────────────────────────

        /// <summary>
        /// Stops active playback, clears the queue, cancels fades, and releases all resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
            _stream.Completed -= OnPlaybackCompleted;
            _stream.Dispose();
        }

        // ── Private ─────────────────────────────────────────────────────────

        /// <summary>Begins playback immediately, sets state, displays the subtitle, and fires the started event.</summary>
        /// <param name="voicelineEvent">The event to play.</param>
        /// <param name="priority">The priority assigned to this playback.</param>
        private void PlayImmediate(VoicelineEvent voicelineEvent, VoicelinePriority priority)
        {
            if (!_stream.Play(voicelineEvent.AudioEvent))
            {
                AudioDiagnostics.Error($"Failed to play '{voicelineEvent.name}'. Was it preloaded?");
                OnVoicelineFailed?.Invoke(voicelineEvent);
                PlayNextQueued();
                return;
            }

            _currentVoiceline = voicelineEvent;
            _currentPriority = priority;

            DisplaySubtitle(voicelineEvent.SubtitleKey);
            OnVoicelineStarted?.Invoke(voicelineEvent);
        }

        /// <summary>Drain policy: when a voiceline finishes naturally, fire completion and play the next queued line.</summary>
        private void OnPlaybackCompleted()
        {
            var completed = _currentVoiceline;
            ClearCurrent();
            OnVoicelineCompleted?.Invoke(completed);
            PlayNextQueued();
        }

        /// <summary>
        /// Stops the active playback and resets current-playback policy state.
        /// Must be called before any explicit stop, skip, or interruption.
        /// </summary>
        private void StopCurrent()
        {
            _stream.Stop();
            ClearCurrent();
        }

        /// <summary>Resets the current voiceline, priority, and subtitle. Does not touch the playback engine.</summary>
        private void ClearCurrent()
        {
            _currentVoiceline = null;
            _currentPriority = null;
            ClearSubtitle();
        }

        /// <summary>Dequeues and plays the next voiceline, or fires <see cref="OnQueueEmpty"/> if the queue is empty.</summary>
        private void PlayNextQueued()
        {
            if (_queue.Count == 0)
            {
                OnQueueEmpty?.Invoke();
                return;
            }

            var next = _queue.Dequeue();
            PlayImmediate(next.VoicelineEvent, next.Priority);
        }

        /// <summary>Returns true when <paramref name="newPriority"/> is strictly higher than the currently playing voiceline's priority.</summary>
        /// <param name="newPriority">The priority of the incoming voiceline.</param>
        private bool ShouldInterrupt(VoicelinePriority newPriority) =>
            _currentPriority.HasValue && newPriority > _currentPriority.Value;

        /// <summary>Looks up the localised text for <paramref name="subtitleKey"/> and writes it to the subtitle display. No-op when the display is unbound or the key is empty.</summary>
        /// <param name="subtitleKey">The localization key to resolve.</param>
        private void DisplaySubtitle(string subtitleKey)
        {
            if (_subtitleDisplay == null || string.IsNullOrEmpty(subtitleKey)) return;
            _subtitleDisplay.text = LocalizationUtility.GetLocalizedText(subtitleKey, _languageSystem.CurrentLanguage);
        }

        /// <summary>Clears the subtitle display text. No-op when the display is unbound.</summary>
        private void ClearSubtitle()
        {
            if (_subtitleDisplay == null) return;
            _subtitleDisplay.text = string.Empty;
        }
    }
}