using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Systems.UI.Core.Transition
{
    /// <summary>
    /// Drives the full-screen fade overlay between opaque and transparent. <see cref="BeginLoading"/>
    /// blocks until the screen is fully black; <see cref="EndLoading"/> fires and forgets the fade back
    /// to clear. Overlapping calls on the same direction are ignored; an in-flight fade in the opposite
    /// direction is cancelled and the new one picks up from the current alpha.
    /// </summary>
    public class TransitionManager
    {
        /// <summary>The canvas group whose alpha is animated between 0 (clear) and 1 (black).</summary>
        private readonly CanvasGroup _overlay;

        /// <summary>Duration in seconds for the fade-to-black (BeginLoading) animation.</summary>
        private readonly float _fadeInDuration;

        /// <summary>Duration in seconds for the fade-to-clear (EndLoading) animation.</summary>
        private readonly float _fadeOutDuration;

        /// <summary>Cancels any fade currently in progress when a new one starts.</summary>
        private CancellationTokenSource _fadeCts;

        /// <summary>The root canvas that hosts the overlay; activated before fading in and deactivated after fading out.</summary>
        private GameObject _canvas;

        /// <summary>True while a BeginLoading fade is in progress or has completed but EndLoading has not been called.</summary>
        public bool IsTransitioning { get; set; }

        /// <summary>
        /// Resolves the overlay canvas group and its parent canvas, and stores the fade durations.
        /// </summary>
        /// <param name="overlay">The MonoBehaviour that exposes the fade canvas group.</param>
        /// <param name="fadeInDuration">Seconds to reach full opacity on <see cref="BeginLoading"/>.</param>
        /// <param name="fadeOutDuration">Seconds to reach full transparency on <see cref="EndLoading"/>.</param>
        public TransitionManager(TransitionOverlay overlay, float fadeInDuration = 0.4f, float fadeOutDuration = 0.6f)
        {
            _canvas = overlay.transform.parent.gameObject;
            _overlay = overlay.CanvasGroup;
            _fadeInDuration = fadeInDuration;
            _fadeOutDuration = fadeOutDuration;
        }

        /// <summary>
        /// Fades the screen to black. Await this before touching any resources.
        /// </summary>
        public async UniTask BeginLoading()
        {
            if (IsTransitioning) return; // Prevent overlapping transitions if BeginLoading is called multiple times in a row.
            
            _canvas.SetActive(true);
            IsTransitioning = true;
            await Fade(to: 1f, duration: _fadeInDuration);
        }

        /// <summary>
        /// Fades the screen back in. Fire-and-forget — the game resumes while
        /// the overlay becomes transparent.
        /// </summary>
        public void EndLoading()
        {
            if (!IsTransitioning) return; // Prevent overlapping transitions if EndLoading is called multiple times in a row.
            
            IsTransitioning = false;
            Fade(to: 0f, duration: _fadeOutDuration).Forget();
        }

        /// <summary>
        /// Tweens the overlay alpha from its current value to <paramref name="to"/> over
        /// <paramref name="duration"/> seconds using unscaled time. Cancels any in-flight tween before
        /// starting. Blocks raycasts for the full tween; releases them and deactivates the canvas only
        /// once the overlay reaches full transparency.
        /// </summary>
        /// <param name="to">Target alpha: 1 for fully opaque (black), 0 for fully transparent.</param>
        /// <param name="duration">Tween duration in seconds.</param>
        private async UniTask Fade(float to, float duration)
        {
            // Cancel any fade already in progress (e.g. EndLoading mid-flight
            // when BeginLoading is called again).
            _fadeCts?.Cancel();
            _fadeCts?.Dispose();
            _fadeCts = new CancellationTokenSource();
            var token = _fadeCts.Token;

            float from = _overlay.alpha;
            float elapsed = 0f;

            // Fully opaque overlay should block raycasts; transparent should not.
            _overlay.blocksRaycasts = true;

            try
            {
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _overlay.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                _overlay.alpha = to;
            }
            catch (System.OperationCanceledException)
            {
                // Another fade took over — leave alpha wherever it landed.
                // The new fade will pick up from there via `from = _overlay.alpha`.
            }
            finally
            {
                // Only release raycast blocking once we're fully transparent.
                if (_overlay.alpha <= 0f)
                {
                    _overlay.blocksRaycasts = false;
                    _canvas.SetActive(false);
                }
            }
        }
    }
}