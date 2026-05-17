using System.Collections.Generic;
using Reflex.Attributes;
using UnityEngine;

namespace Systems.Core
{
    public class TickManager : MonoBehaviour
    {
        [Inject] private readonly IEnumerable<ITickable<TickManager>> _tickables;
        [Inject] private readonly IEnumerable<IInterpolatable> _interpolatables;

        public const int TickRate = 60; // Ticks per second

        private float _accumulator = 0.0f;
        public static float TickInterval => 1.0f / TickRate;

        private float _timeScale = 1.0f;

        private bool _autoTick = true;

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

        private void AdvanceTick()
        {
            foreach (var t in _tickables) t.InputTick();
            foreach (var t in _tickables) t.LogicTick();
            foreach (var t in _tickables) t.UITick();
        }

        private void Interpolate(float alpha)
        {
            foreach (var i in _interpolatables) i.Interpolate(alpha);
        }

        public void SetTimeScale(float timeScale)
        {
            _timeScale = timeScale;
        }

        public void SetAutoTick(bool enabled)
        {
            _autoTick = enabled;
        }

        public void ForceTickAndInterpolate()
        {
            if (_autoTick) return;

            AdvanceTick();
            Interpolate(1.0f); // Force interpolation to the end of the tick
        }
    }
}