using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Systems.UI.Transition
{
    public class TransitionManager
    {
        private readonly CanvasGroup _overlay;
        private readonly float _fadeInDuration;
        private readonly float _fadeOutDuration;

        private CancellationTokenSource _fadeCts;

        private GameObject _canvas;
        
        public bool IsTransitioning { get; set; }

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