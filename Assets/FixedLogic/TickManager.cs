using System.Collections.Generic;
using UnityEngine;

namespace FixedLogic
{
    public class TickManager : MonoBehaviour
    {
        public const int TickRate = 60; // Ticks per second
        
        private float _accumulator = 0.0f;
        private const float TickInterval = 1.0f / TickRate;

        private readonly List<ITickable> _tickables = new();
        private readonly List<IInterpolatable> _interpolatables = new();

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

        public void Register(object obj)
        {
            if (obj is ITickable t) _tickables.Add(t);
            if (obj is IInterpolatable i) _interpolatables.Add(i);
        }

        public void Unregister(object obj)
        {
            if (obj is ITickable t) _tickables.Remove(t);
            if (obj is IInterpolatable i) _interpolatables.Remove(i);
        }
    }
}