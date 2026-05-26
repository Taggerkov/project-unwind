using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Systems.Input
{
    /// <summary>
    /// The PlayerLinker component serves as a bridge between player controlled systems and the rest of the game.
    /// </summary>
    public class PlayerLinker : MonoBehaviour
    {
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private MultiplayerEventSystem multiplayerEventSystem;
        [SerializeField] private InputSystemUIInputModule inputSystemUIInputModule;

        public PlayerInput PlayerInput => playerInput;
        public MultiplayerEventSystem MultiplayerEventSystem => multiplayerEventSystem;

        public InputSystemUIInputModule InputSystemUIInputModule => inputSystemUIInputModule;

        public int PlayerId => playerInput.playerIndex;

        public PlayerInputProvider PlayerInputProvider { get; private set; }

        /// <summary>
        /// Called when the player submits a UI action. Provides the PlayerLinker and the currently selected GameObject.
        /// </summary>
        public event Action<PlayerLinker, Selectable> OnUISubmit;

        /// <summary>
        /// Called when the player navigates the UI. Provides the PlayerLinker, the previous Selectable, and the new Selectable.
        /// </summary>
        public event Action<PlayerLinker, Selectable, Selectable> OnUINavigate;

        private InputAction _navigateAction;
        private InputAction _submitAction;

        private Action<InputAction.CallbackContext> _uiNavigateHandler;

        private void OnValidate()
        {
            if (!playerInput)
            {
                Debug.LogWarning("PlayerLinker: PlayerInput reference is not set.");
            }

            if (!multiplayerEventSystem)
            {
                Debug.LogWarning("PlayerLinker: MultiplayerEventSystem reference is not set.");
            }

            if (!inputSystemUIInputModule)
            {
                Debug.LogWarning("PlayerLinker: InputSystemUIInputModule reference is not set.");
            }
        }

        private void OnEnable()
        {
            _navigateAction = playerInput.actions["Navigate"];

            _uiNavigateHandler = context => OnPlayerNavigate(context).Forget();

            if (_navigateAction != null)
            {
                _navigateAction.performed += _uiNavigateHandler;
            }
            else
            {
                Debug.LogError("PlayerLinker: 'Navigate' action not found in PlayerInput actions.");
            }

            _submitAction = playerInput.actions["Submit"];

            if (_submitAction != null)
            {
                _submitAction.performed += OnPlayerSubmit;
            }
            else
            {
                Debug.LogError("PlayerLinker: 'Submit' action not found in PlayerInput actions.");
            }
        }

        private void OnDisable()
        {
            if (_navigateAction != null)
            {
                _navigateAction.performed -= _uiNavigateHandler;
            }

            if (_submitAction != null)
            {
                _submitAction.performed -= OnPlayerSubmit;
            }
        }

        private void Awake()
        {
            PlayerInputProvider = new PlayerInputProvider(playerInput);
            inputSystemUIInputModule.moveRepeatDelay = float.MaxValue;
        }

        private async UniTaskVoid OnPlayerNavigate(InputAction.CallbackContext context)
        {
            var previousSelectedObject = multiplayerEventSystem.currentSelectedGameObject;

            await UniTask.WaitForEndOfFrame();

            GameObject currentObject = multiplayerEventSystem.currentSelectedGameObject;

            if (currentObject && previousSelectedObject != currentObject &&
                currentObject.TryGetComponent<Selectable>(out var newSelectable))
            {
                if (previousSelectedObject &&
                    previousSelectedObject.TryGetComponent<Selectable>(out var previousSelectable))
                {
                    OnUINavigate?.Invoke(this, previousSelectable, newSelectable);
                }
                else
                {
                    OnUINavigate?.Invoke(this, null, newSelectable);
                }
            }
        }

        private void OnPlayerSubmit(InputAction.CallbackContext context)
        {
            var currentSelected = multiplayerEventSystem.currentSelectedGameObject;
            if (currentSelected && currentSelected.TryGetComponent<Selectable>(out var selectable))
            {
                OnUISubmit?.Invoke(this, selectable);
            }
        }
    }
}