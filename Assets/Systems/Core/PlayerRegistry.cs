using System;
using System.Collections.Generic;
using Systems.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems.Core
{
    public class PlayerRegistry : IDisposable
    {
        public event Action<PlayerLinker> OnPlayerJoined;
        public event Action<PlayerLinker> OnPlayerLeft;

        private readonly PlayerInputManager _playerInputManager;

        private PlayerLinker _player0Linker;
        private PlayerLinker _player1Linker;

        public PlayerRegistry(PlayerInputManager playerInputManager)
        {
            _playerInputManager = playerInputManager;
            _playerInputManager.onPlayerJoined += HandlePlayerJoined;
            _playerInputManager.onPlayerLeft += HandlePlayerLeft;
        }

        public void Dispose()
        {
            _playerInputManager.onPlayerJoined -= HandlePlayerJoined;
            _playerInputManager.onPlayerLeft -= HandlePlayerLeft;
            Debug.Log("PlayerRegistry: Dispose()");
        }

        
        public List<PlayerLinker> GetAllPlayers()
        {
            var players = new List<PlayerLinker>();
            if (_player0Linker) players.Add(_player0Linker);
            if (_player1Linker) players.Add(_player1Linker);
            return players;
        }

        private void HandlePlayerJoined(PlayerInput playerInput)
        {
            Debug.Log($"Player {playerInput.playerIndex} Joined!");
            
            var linker = playerInput.GetComponent<PlayerLinker>();
            
            if (!linker)
            {
                Debug.LogError("PlayerRegistry: PlayerInput does not have a PlayerLinker component!");
                return;
            }
            
            if (playerInput.playerIndex == 0)
            {
                _player0Linker = linker;
            }
            else if (playerInput.playerIndex == 1)
            {
                _player1Linker = linker;
            }
            else
            {
                Debug.LogError("PlayerRegistry: More than 2 players are not supported!");
                return;
            }

            OnPlayerJoined?.Invoke(linker);
        }

        private void HandlePlayerLeft(PlayerInput playerInput)
        {
            Debug.Log($"Player {playerInput.playerIndex} Left!");

            var linker = playerInput.GetComponent<PlayerLinker>();

            if (playerInput.playerIndex == 0) _player0Linker = null;
            else if (playerInput.playerIndex == 1) _player1Linker = null;

            if (linker) OnPlayerLeft?.Invoke(linker);
        }
    }
}