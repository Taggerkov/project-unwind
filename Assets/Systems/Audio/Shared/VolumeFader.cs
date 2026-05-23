using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.Audio.Contracts;
using UnityEngine;

namespace Systems.Audio.Shared
{
    /// <summary>
    /// Applies immediate or linearly-faded volume changes to either a single playback handle or an entire category.
    /// Each fade reads its start value from the live target, so a single instance may safely drive different
    /// targets over its lifetime and stays correct even if the target volume was changed by another caller.
    /// Only one fade runs at a time; starting a new fade or immediate set cancels the one in flight.
    /// </summary>
    public sealed class VolumeFader : IDisposable
    {
        /// <summary>The audio playback surface used to apply and query volume changes.</summary>
        private readonly AudioManager _audioManager;

        /// <summary>Cancels the active <see cref="FadeInternalAsync"/> task when a new fade or immediate set begins.</summary>
        private CancellationTokenSource _fadeCts;

        /// <summary>The last volume value applied by this fader.</summary>
        public float Volume { get; private set; } = 1f;

        /// <summary>Constructs the fader. Pass the same <see cref="AudioManager"/> instance used by the owning system.</summary>
        /// <param name="audioManager">The audio playback surface used to apply volume changes.</param>
        public VolumeFader(AudioManager audioManager) => _audioManager = audioManager;

        // ── Category ─────────────────────────────────────────────────────────

        /// <summary>Sets the volume for <paramref name="category"/> immediately. Cancels any running fade.</summary>
        /// <param name="category">The audio category to target.</param>
        /// <param name="volume">Target volume. Clamped to a zero minimum.</param>
        public void Set(AudioCategory category, float volume)
        {
            CancelAndDispose(ref _fadeCts);
            Volume = Mathf.Max(0f, volume);
            _audioManager.SetCategoryVolume(category, Volume);
        }

        /// <summary>Linearly fades the volume for <paramref name="category"/> from its current value to <paramref name="target"/>.</summary>
        /// <param name="category">The audio category to target.</param>
        /// <param name="target">Target volume. Clamped to a zero minimum.</param>
        /// <param name="duration">Fade duration in seconds. Zero or negative applies <paramref name="target"/> immediately.</param>
        public UniTask FadeToAsync(AudioCategory category, float target, float duration)
        {
            CancelAndDispose(ref _fadeCts);
            _fadeCts = new CancellationTokenSource();
            return FadeInternalAsync(
                v => _audioManager.SetCategoryVolume(category, v),
                _audioManager.GetCategoryVolume(category),
                Mathf.Max(0f, target), duration, _fadeCts.Token);
        }

        // ── Handle ───────────────────────────────────────────────────────────

        /// <summary>Sets the volume on the playback identified by <paramref name="uuid"/> immediately. Cancels any running fade.</summary>
        /// <param name="uuid"><see cref="Guid"/> returned by <see cref="AudioManager.Play"/>.</param>
        /// <param name="volume">Target volume. Clamped to a zero minimum.</param>
        public void Set(Guid uuid, float volume)
        {
            CancelAndDispose(ref _fadeCts);
            Volume = Mathf.Max(0f, volume);
            _audioManager.SetVolume(uuid, Volume);
        }

        /// <summary>Linearly fades the volume on the playback identified by <paramref name="uuid"/> from its current value to <paramref name="target"/>.</summary>
        /// <param name="uuid"><see cref="Guid"/> returned by <see cref="AudioManager.Play"/>.</param>
        /// <param name="target">Target volume. Clamped to a zero minimum.</param>
        /// <param name="duration">Fade duration in seconds. Zero or negative applies <paramref name="target"/> immediately.</param>
        public UniTask FadeToAsync(Guid uuid, float target, float duration)
        {
            CancelAndDispose(ref _fadeCts);
            _fadeCts = new CancellationTokenSource();
            return FadeInternalAsync(
                v => _audioManager.SetVolume(uuid, v),
                _audioManager.GetVolume(uuid),
                Mathf.Max(0f, target), duration, _fadeCts.Token);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        /// <summary>Cancels any in-flight fade.</summary>
        public void Dispose() => CancelAndDispose(ref _fadeCts);

        // ── Private ───────────────────────────────────────────────────────────

        /// <summary>
        /// Lerps from <paramref name="start"/> to <paramref name="target"/> over <paramref name="duration"/> seconds,
        /// calling <paramref name="apply"/> each frame and tracking the latest value in <see cref="Volume"/>.
        /// Applies <paramref name="target"/> immediately if <paramref name="duration"/> is zero or negative.
        /// Exits silently on cancellation without applying the final value.
        /// </summary>
        /// <param name="apply">Delegate that forwards the interpolated volume to the audio backend.</param>
        /// <param name="start">The volume to fade from, read from the live target at call time.</param>
        /// <param name="target">The volume to fade to. Must be zero or greater (clamped by callers).</param>
        /// <param name="duration">Fade duration in seconds.</param>
        /// <param name="ct">Token to abort the fade mid-lerp.</param>
        private async UniTask FadeInternalAsync(Action<float> apply, float start, float target, float duration, CancellationToken ct)
        {
            if (duration <= 0f)
            {
                Volume = target;
                apply(Volume);
                return;
            }

            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Volume = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                apply(Volume);
                var cancelled = await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow();
                if (cancelled) return;
            }

            Volume = target;
            apply(Volume);
        }

        /// <summary>Cancels and disposes <paramref name="cts"/>, then sets it to null. Safe to call when already null.</summary>
        /// <param name="cts">The token source to tear down.</param>
        private static void CancelAndDispose(ref CancellationTokenSource cts)
        {
            if (cts == null) return;
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }
    }
}