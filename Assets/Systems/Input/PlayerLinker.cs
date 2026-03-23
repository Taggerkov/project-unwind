using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems.Input
{
    public class PlayerLinker : MonoBehaviour
    {
        public PlayerInput PlayerInput { get; private set; }

        public int PlayerId => PlayerInput.playerIndex;

        public PlayerInputProvider PlayerInputProvider { get; private set; }

        public void Setup(PlayerInput playerInput)
        {
            PlayerInput = playerInput;

            PlayerInputProvider = new PlayerInputProvider(playerInput);
        }
    }
}