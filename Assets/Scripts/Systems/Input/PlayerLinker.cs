using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems.Input
{
    public class PlayerLinker : MonoBehaviour
    {
        public PlayerInput PlayerInput { get; private set; }

        public int PlayerId => PlayerInput.playerIndex;

        public InputHandler InputHandler { get; private set; }

        public void Setup(PlayerInput playerInput)
        {
            PlayerInput = playerInput;

            InputHandler = new InputHandler(playerInput);
        }
    }
}