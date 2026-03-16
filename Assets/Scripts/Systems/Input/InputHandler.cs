using FixedLogic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems.Input
{
    
    /// <summary>
    /// 
    /// ...in a given frame.
    /// </summary>
    public struct FrameInput
    {
        public int Direction; // 1-9
        public ButtonState LightAttack;
        public ButtonState MediumAttack;
        public ButtonState HeavyAttack;
        public ButtonState UniqueAttack;
        public ButtonState GuardButton;
        public ButtonState AbilityButton;

        public bool IsNeutral => Direction == 5 && !LightAttack.Held && !MediumAttack.Held && !HeavyAttack.Held &&
                                 !UniqueAttack.Held && !GuardButton.Held && !AbilityButton.Held;
    }

    public struct ButtonState
    {
        public bool Pressed; // True only on the first frame of the press
        public bool Held; // True every frame the button is down
        public bool Released; // True only on the first frame of the release
    }

    public class ButtonTracker
    {
        private bool _physicalDown;
        private bool _latchedPress;
        private bool _lastFrameHeld;

        public void LinkAction(InputAction action)
        {
            action.started += ctx =>
            {
                _physicalDown = true;
                _latchedPress = true;
            };
            action.canceled += ctx => _physicalDown = false;
        }

        public ButtonState GetStateAndStep()
        {
            bool currentDown = _physicalDown || _latchedPress;
            ButtonState state = new ButtonState
            {
                Pressed = currentDown && !_lastFrameHeld,
                Held = currentDown,
                Released = !currentDown && _lastFrameHeld
            };

            _lastFrameHeld = currentDown;
            _latchedPress = false;
            return state;
        }
    }

    public class InputHandler : ITickable
    {
        public int PlayerId { get; private set; }
        public string DeviceName { get; private set; }
        public string ControlScheme { get; private set; }
        
        public delegate void NewFrameHandler(FrameInput frame);
        public event NewFrameHandler OnNewFrame;

        private PlayerInput _pi;

        private InputAction _directionalInputAction;
        private InputAction _lightAttackAction;
        private InputAction _mediumAttackAction;
        private InputAction _heavyAttackAction;
        private InputAction _uniqueAttackAction;
        private InputAction _guardButtonAction;
        private InputAction _abilityButtonAction;

        private ButtonTracker _lightAttackButtonTracker;
        private ButtonTracker _mediumAttackButtonTracker;
        private ButtonTracker _heavyAttackButtonTracker;
        private ButtonTracker _uniqueAttackButtonTracker;
        private ButtonTracker _guardButtonTracker;
        private ButtonTracker _abilityButtonTracker;
        
        public InputBuffer Buffer { get; private set; } = new();

        //Accumulator for inputs. Checks if an input was registered between the fixed 60HZ ticks.

        private Vector2 _latchedDirection;

        public InputHandler(PlayerInput playerInput)
        {
            _pi = playerInput;

            PlayerId = playerInput.playerIndex;
            DeviceName = playerInput.devices.Count > 0 ? playerInput.devices[0].displayName : "Unknown";
            ControlScheme = playerInput.currentControlScheme;

            // Cache the actions from the asset instance
            _directionalInputAction = playerInput.actions.FindAction("DirectionalInput");
            _lightAttackAction = playerInput.actions.FindAction("LightAttack");
            _mediumAttackAction = playerInput.actions.FindAction("MediumAttack");
            _heavyAttackAction = playerInput.actions.FindAction("HeavyAttack");
            _uniqueAttackAction = playerInput.actions.FindAction("UniqueAttack");
            _guardButtonAction = playerInput.actions.FindAction("GuardButton");
            _abilityButtonAction = playerInput.actions.FindAction("AbilityButton");

            _lightAttackButtonTracker = new ButtonTracker();
            _mediumAttackButtonTracker = new ButtonTracker();
            _heavyAttackButtonTracker = new ButtonTracker();
            _uniqueAttackButtonTracker = new ButtonTracker();
            _guardButtonTracker = new ButtonTracker();
            _abilityButtonTracker = new ButtonTracker();

            _lightAttackButtonTracker.LinkAction(_lightAttackAction);
            _mediumAttackButtonTracker.LinkAction(_mediumAttackAction);
            _heavyAttackButtonTracker.LinkAction(_heavyAttackAction);
            _uniqueAttackButtonTracker.LinkAction(_uniqueAttackAction);
            _guardButtonTracker.LinkAction(_guardButtonAction);
            _abilityButtonTracker.LinkAction(_abilityButtonAction);

            _directionalInputAction.performed += ctx =>
            {
                Vector2 val = ctx.ReadValue<Vector2>();
                // If the new input is "stronger" (further from neutral), latch it
                if (val.sqrMagnitude > _latchedDirection.sqrMagnitude)
                    _latchedDirection = val;
            };
        }

        public void InputTick()
        {
            // 1. Resolve Direction
            int direction = InputUtils.VectorToNumpad(_latchedDirection);
            if (direction == 5)
                direction = InputUtils.VectorToNumpad(_directionalInputAction.ReadValue<Vector2>());

            // 2. Build the FrameInput snapshot
            FrameInput currentFrame = new FrameInput
            {
                Direction = direction,
                LightAttack = _lightAttackButtonTracker.GetStateAndStep(),
                MediumAttack = _mediumAttackButtonTracker.GetStateAndStep(),
                HeavyAttack = _heavyAttackButtonTracker.GetStateAndStep(),
                UniqueAttack = _uniqueAttackButtonTracker.GetStateAndStep(),
                GuardButton = _guardButtonTracker.GetStateAndStep(),
                AbilityButton = _abilityButtonTracker.GetStateAndStep()
            };
            
            Buffer.Write(currentFrame);
            
            OnNewFrame?.Invoke(currentFrame);

            _latchedDirection = Vector2.zero; // Clear latched direction after processing
        }
    }
}