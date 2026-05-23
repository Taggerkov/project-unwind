using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.UI.Combat.HealthBar
{
    public class CombatUIHealthBarController : MonoBehaviour
    {
        [SerializeField] private Material healthBarMaterial;

        private Material _materialInstance; // Instance material on the UI Image

        [SerializeField] private Image image;

        [Header("Catchup")] [SerializeField]
        private float _catchupDelay = 0.8f; // Seconds after hitstun ends before bar moves

        [SerializeField] private float _catchupSpeed = 0.25f; // Bar fraction drained per second

        private static readonly int HealthId = Shader.PropertyToID("_Health");
        private static readonly int HealthCatchupId = Shader.PropertyToID("_HealthCatchup");

        private float _currentHealth = 1f;
        private float _catchupHealth = 1f;
        private CancellationTokenSource _cts;

        private void OnValidate()
        {
            image = GetComponent<Image>();
        }

        private void Awake()
        {
            // Create an instance of the material so we can modify it without affecting other UI elements using the same shader.
            _materialInstance = Instantiate(healthBarMaterial);
            image.material = _materialInstance;

            // Initialize shader properties
            _materialInstance.SetFloat(HealthId, _currentHealth);
            _materialInstance.SetFloat(HealthCatchupId, _catchupHealth);
        }

        // ── Public API ────────────────────────────────────────────────────────────

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

        private void CancelCatchup()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void OnDestroy() => CancelCatchup();
    }
}