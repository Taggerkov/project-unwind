using System;
using System.Collections.Generic;
using Systems.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems.Core
{
    /// <summary>
    /// Tracks the two <see cref="PlayerLinker"/> instances that join and leave through Unity's
    /// <see cref="PlayerInputManager"/> and dispatches typed events to subscribers (e.g.
    /// <see cref="UIManager"/>). Supports at most two simultaneous players.
    /// </summary>
    public class PlayerRegistry : IDisposable
    {
        /// <summary>Raised when a player joins; provides the player's <see cref="PlayerLinker"/>.</summary>
        public event Action<PlayerLinker> OnPlayerJoined;

        /// <summary>Raised when a player leaves; provides the player's <see cref="PlayerLinker"/>.</summary>
        public event Action<PlayerLinker> OnPlayerLeft;

        /// <summary>Unity's input manager used to detect join and leave events.</summary>
        private readonly PlayerInputManager _playerInputManager;

        /// <summary>Linker for player index 0; null when that slot is unoccupied.</summary>
        private PlayerLinker _player0Linker;

        /// <summary>Linker for player index 1; null when that slot is unoccupied.</summary>
        private PlayerLinker _player1Linker;

        /// <summary>Subscribes to the PlayerInputManager join and leave callbacks.</summary>
        /// <param name="playerInputManager">The scene's PlayerInputManager component.</param>
        public PlayerRegistry(PlayerInputManager playerInputManager)
        {
            _playerInputManager = playerInputManager;
            _playerInputManager.onPlayerJoined += HandlePlayerJoined;
            _playerInputManager.onPlayerLeft += HandlePlayerLeft;
        }

        /// <summary>Unsubscribes from the PlayerInputManager callbacks.</summary>
        public void Dispose()
        {
            _playerInputManager.onPlayerJoined -= HandlePlayerJoined;
            _playerInputManager.onPlayerLeft -= HandlePlayerLeft;
            Debug.Log("PlayerRegistry: Dispose()");
        }

        /// <summary>Returns a list of all currently joined players in join order.</summary>
        /// <returns>A list containing the linkers for slots 0 and 1 that are currently occupied.</returns>
        public List<PlayerLinker> GetAllPlayers()
        {
            var players = new List<PlayerLinker>();
            if (_player0Linker) players.Add(_player0Linker);
            if (_player1Linker) players.Add(_player1Linker);
            return players;
        }

        /// <summary>
        /// Resolves the <see cref="PlayerLinker"/> on the joining PlayerInput, slots it by
        /// player index, and raises <see cref="OnPlayerJoined"/>.
        /// </summary>
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

        /// <summary>
        /// Clears the departing player's slot and raises <see cref="OnPlayerLeft"/> if a linker
        /// was present.
        /// </summary>
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