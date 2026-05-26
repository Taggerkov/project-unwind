using System.Collections.Generic;
using Reflex.Attributes;
using UnityEngine;

namespace Systems.Core
{
    /// <summary>
    /// MonoBehaviour that drives the fixed-rate 60 Hz game tick loop independent of Unity's
    /// frame rate. Each Unity Update accumulates delta time and fires complete ticks in three
    /// ordered phases — InputTick, LogicTick, UITick — then calls Interpolate with the
    /// remaining sub-tick alpha for smooth rendering.
    /// </summary>
    public class TickManager : MonoBehaviour
    {
        /// <summary>All systems registered as <see cref="ITickable{TickManager}"/> via Reflex injection.</summary>
        [Inject] private readonly IEnumerable<ITickable<TickManager>> _tickables;

        /// <summary>All systems that support sub-tick interpolation via Reflex injection.</summary>
        [Inject] private readonly IEnumerable<IInterpolatable> _interpolatables;

        /// <summary>Number of simulation ticks per real second.</summary>
        public const int TickRate = 60;

        /// <summary>Accumulated unprocessed time in seconds since the last complete tick.</summary>
        private float _accumulator = 0.0f;

        /// <summary>Duration of one simulation tick in seconds.</summary>
        public static float TickInterval => 1.0f / TickRate;

        /// <summary>Multiplier applied to delta time before accumulation; used for hitstop.</summary>
        private float _timeScale = 1.0f;

        /// <summary>When false, automatic ticking is suspended; advance manually via <see cref="ForceTickAndInterpolate"/>.</summary>
        private bool _autoTick = true;

        /// <summary>
        /// Accumulates scaled delta time and drains it in whole ticks, then interpolates
        /// with the remaining sub-tick fraction.
        /// </summary>
        private void Update()
        {
            if (!_autoTick) return;

            _accumulator += Time.deltaTime * _timeScale;

            while (_accumulator >= TickInterval)
            {
                AdvanceTick();
                _accumulator -= TickInterval;
            }

            float alpha = _accumulator / TickInterval;
            Interpolate(alpha);
        }

        /// <summary>
        /// Executes one complete simulation tick across all tickables in phase order:
        /// InputTick, LogicTick, UITick.
        /// </summary>
        private void AdvanceTick()
        {
            foreach (var t in _tickables) t.InputTick();
            foreach (var t in _tickables) t.LogicTick();
            foreach (var t in _tickables) t.UITick();
        }

        /// <summary>Calls <see cref="IInterpolatable.Interpolate"/> on every registered interpolatable.</summary>
        /// <param name="alpha">Sub-tick fraction in [0, 1] used to blend between the previous and current tick state.</param>
        private void Interpolate(float alpha)
        {
            foreach (var i in _interpolatables) i.Interpolate(alpha);
        }

        /// <summary>Sets the time scale applied to delta time before accumulation. Use values below 1 for hitstop.</summary>
        /// <param name="timeScale">Scale factor; 1.0 is real time, 0.0 is fully frozen.</param>
        public void SetTimeScale(float timeScale)
        {
            _timeScale = timeScale;
        }

        /// <summary>
        /// Enables or disables automatic ticking from <c>Update</c>. When disabled, call
        /// <see cref="ForceTickAndInterpolate"/> to advance manually.
        /// </summary>
        /// <param name="enabled">True to resume automatic ticking; false to pause it.</param>
        public void SetAutoTick(bool enabled)
        {
            _autoTick = enabled;
        }

        /// <summary>
        /// Fires one tick and interpolates to the end of that tick. Only valid when auto-tick is
        /// disabled; no-op otherwise to prevent double-advancing.
        /// </summary>
        public void ForceTickAndInterpolate()
        {
            if (_autoTick) return;

            AdvanceTick();
            Interpolate(1.0f);
        }
    }
}