using Systems.Combat.Core;
using Systems.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems.Core
{
    public class PlayerRegistry : MonoBehaviour
    {
        private GameManager _gameManager;
        private PlayerInputManager _playerInputManager;

        private void Awake()
        {
            //Assume all these components live in the same "GlobalSystems" prefab.
            _gameManager = GetComponent<GameManager>();
            _playerInputManager = GetComponent<PlayerInputManager>();
        }

        private void OnEnable()
        {
            _playerInputManager.onPlayerJoined += OnPlayerJoined;
        }

        private void OnDisable()
        {
            _playerInputManager.onPlayerJoined -= OnPlayerJoined;
        }

        public void OnPlayerJoined(PlayerInput playerInput)
        {
            Debug.Log($"Player {playerInput.playerIndex + 1} Joined!");
            
            var linker = playerInput.GetComponent<PlayerLinker>();
            
            if (!linker)
            {
                Debug.LogError("PlayerRegistry: PlayerInput does not have a PlayerLinker component!");
                return;
            }

            linker.Setup(playerInput);
        
            // Register it to the 60Hz tick system
            _gameManager.AddPlayer(linker.PlayerId, linker);
        }
    }
}