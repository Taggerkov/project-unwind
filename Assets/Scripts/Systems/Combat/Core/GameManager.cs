using FixedLogic;
using JetBrains.Annotations;
using Systems.Input;
using UnityEngine;

namespace Systems.Combat.Core
{
    public class GameManager : SingletonMono<GameManager>

    {
        [SerializeField] private TickManager tickManager;

        [CanBeNull] private PlayerLinker _player0Linker;
        [CanBeNull] private PlayerLinker _player1Linker;
        
        public event System.Action<PlayerLinker> OnPlayerJoin;

        [CanBeNull]
        public PlayerLinker GetPlayerWithIndex(int index)
        {
            return index switch
            {
                0 => _player0Linker,
                1 => _player1Linker,
                _ => null
            };
        }

        public void Register(ITickable tickable)
        {
            tickManager.Register(tickable);
        }

        public void Register(IInterpolatable interpolatable)
        {
            tickManager.Register(interpolatable);
        }

        public void Unregister(ITickable tickable)
        {
            tickManager.Unregister(tickable);
        }
        
        public void Unregister(IInterpolatable interpolatable)
        {
            tickManager.Unregister(interpolatable);
        }

        public void AddPlayer(int index, PlayerLinker linker)
        {
            switch (index)
            {
                case 0 when !_player0Linker:
                    _player0Linker = linker;
                    tickManager.Register(linker.InputHandler);
                    OnPlayerJoin?.Invoke(linker);
                    break;
                case 1 when !_player1Linker:
                    _player1Linker = linker;
                    tickManager.Register(linker.InputHandler);
                    OnPlayerJoin?.Invoke(linker);
                    break;
                default:
                    Debug.LogError($"GameManager: Invalid player index {index} for registration!");
                    break;
            }
        }
    }
}