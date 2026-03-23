using System.Collections.Generic;
using FixedLogic;
using Reflex.Attributes;
using UnityEngine;

namespace Systems.Core
{
    public class TickManager : MonoBehaviour
    {
        [Inject] private readonly IEnumerable<ITickable> _tickables;
        [Inject] private readonly IEnumerable<IInterpolatable> _interpolatables;

        public const int TickRate = 60; // Ticks per second

        private float _accumulator = 0.0f;
        public const float TickInterval = 1.0f / TickRate;

        private void Update()
        {
            _accumulator += Time.deltaTime;

            while (_accumulator >= TickInterval)
            {
                foreach (var t in _tickables) t.InputTick();
                foreach (var t in _tickables) t.LogicTick();
                foreach (var t in _tickables) t.UITick();

                _accumulator -= TickInterval;
            }

            float alpha = _accumulator / TickInterval;
            foreach (var i in _interpolatables) i.Interpolate(alpha);
        }
    }
}