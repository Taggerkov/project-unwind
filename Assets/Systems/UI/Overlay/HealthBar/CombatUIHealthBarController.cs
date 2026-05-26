using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.UI.Overlay.HealthBar
{
    /// <summary>
    /// Controls a two-layer health bar shader: a green bar tracking current HP and a red
    /// catchup bar that drains to meet it after a delay when hitstun ends. Each instance
    /// owns a private material clone so multiple bars can animate independently.
    /// </summary>
    public class CombatUIHealthBarController : MonoBehaviour
    {
        /// <summary>Source material for the health bar shader; cloned at Awake so instances are independent.</summary>
        [SerializeField] private Material healthBarMaterial;

        /// <summary>Per-instance material clone written to the Image component at Awake.</summary>
        private Material _materialInstance;

        /// <summary>Image component whose material is replaced with the per-instance clone.</summary>
        [SerializeField] private Image image;

        /// <summary>Seconds after hitstun ends before the red catchup bar begins moving.</summary>
        [Header("Catchup")] [SerializeField]
        private float _catchupDelay = 0.8f;

        /// <summary>Speed at which the red catchup bar drains toward the green bar, in bar fraction per second.</summary>
        [SerializeField] private float _catchupSpeed = 0.25f;

        /// <summary>Cached shader property ID for the <c>_Health</c> green bar value.</summary>
        private static readonly int HealthId = Shader.PropertyToID("_Health");

        /// <summary>Cached shader property ID for the <c>_HealthCatchup</c> red bar value.</summary>
        private static readonly int HealthCatchupId = Shader.PropertyToID("_HealthCatchup");

        /// <summary>Current green bar fraction; always in [0, 1].</summary>
        private float _currentHealth = 1f;

        /// <summary>Current red catchup bar fraction; drains toward <see cref="_currentHealth"/> asynchronously.</summary>
        private float _catchupHealth = 1f;

        /// <summary>Token source for the in-flight catchup coroutine; null when no drain is running.</summary>
        private CancellationTokenSource _cts;

        /// <summary>Ensures the Image reference is populated in the Inspector.</summary>
        private void OnValidate()
        {
            image = GetComponent<Image>();
        }

        /// <summary>
        /// Clones the source material so this bar's shader properties are isolated from other
        /// health bar instances that share the same material asset, then initialises both
        /// shader values to full health.
        /// </summary>
        private void Awake()
        {
            // Create an instance of the material so we can modify it without affecting other UI elements using the same shader.
            _materialInstance = Instantiate(healthBarMaterial);
            image.material = _materialInstance;

            _materialInstance.SetFloat(HealthId, _currentHealth);
            _materialInstance.SetFloat(HealthCatchupId, _catchupHealth);
        }

        /// <summary>
        /// Snaps both the green and red bars to <paramref name="fraction"/> immediately,
        /// cancelling any in-flight catchup animation.
        /// </summary>
        /// <param name="fraction">Target health fraction; clamped to [0, 1].</param>
        public void SetInstantHealthFraction(float fraction)
        {
            _currentHealth = Mathf.Clamp01(fraction);
            _catchupHealth = _currentHealth;
            _materialInstance.SetFloat(HealthId, _currentHealth);
            _materialInstance.SetFloat(HealthCatchupId, _catchupHealth);
            CancelCatchup();
        }

        /// <summary>
        /// Called on hit. Instantly moves the green bar; cancels any in-flight catchup
        /// so it can restart fresh when hitstun eventually ends.
        /// </summary>
        public void OnHealthChanged(float newFraction)
        {
            _currentHealth = Mathf.Clamp01(newFraction);
            _materialInstance.SetFloat(HealthId, _currentHealth);

            // Stop the drain — it will restart from OnHitstunEnded once the combo ends.
            CancelCatchup();
        }

        /// <summary>
        /// Called when the character exits hitstun. Starts the delay, then drains the
        /// red section until it matches the green bar.
        /// </summary>
        public void OnHitstunEnded()
        {
            CancelCatchup();
            _cts = new CancellationTokenSource();
            DrainCatchupAsync(_cts.Token).Forget();
        }

        // ── Catchup logic ─────────────────────────────────────────────────────────

        private async UniTaskVoid DrainCatchupAsync(CancellationToken ct)
        {
            // Wait before the red bar begins to move
            await UniTask.Delay(
                TimeSpan.FromSeconds(_catchupDelay),
                ignoreTimeScale: true, // immune to hitstop
                cancellationToken: ct);

            // Drain in real time (unscaledDeltaTime keeps it immune to hitstop)
            while (_catchupHealth > _currentHealth + Mathf.Epsilon)
            {
                _catchupHealth = Mathf.MoveTowards(
                    _catchupHealth,
                    _currentHealth,
                    _catchupSpeed * Time.unscaledDeltaTime);

                _materialInstance.SetFloat(HealthCatchupId, _catchupHealth);

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            // Snap to eliminate floating-point drift
            _catchupHealth = _currentHealth;
            _materialInstance.SetFloat(HealthCatchupId, _catchupHealth);
        }

        /// <summary>Cancels and disposes the in-flight catchup token source, stopping any running drain.</summary>
        private void CancelCatchup()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        /// <summary>Ensures the catchup task is cancelled when the component is destroyed.</summary>
        private void OnDestroy() => CancelCatchup();
    }
}