using System.Collections.Generic;
using UnityEngine;

namespace FixedLogic
{
    public class TickManager : MonoBehaviour
    {
        public const int TickRate = 120; // Ticks per second
        
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

            var alpha = _accumulator / TickInterval;
            foreach (var i in _interpolatables) i.Interpolate(alpha);
        }

        public void Register(object obj)
        {
            switch (obj)
            {
                case ITickable t:
                    _tickables.Add(t);
                    break;
                case IInterpolatable i:
                    _interpolatables.Add(i);
                    break;
            }
        }

        public void Unregister(object obj)
        {
            switch (obj)
            {
                case ITickable t:
                    _tickables.Remove(t);
                    break;
                case IInterpolatable i:
                    _interpolatables.Remove(i);
                    break;
            }
        }
    }
}