using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems.Input
{
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

        /// <summary>Drops any latched/held state so stale presses do not carry over.</summary>
        public void Reset()
        {
            _physicalDown = false;
            _latchedPress = false;
            _lastFrameHeld = false;
        }
    }

    public class PlayerInputProvider : IInputProvider
    {
        public int PlayerId { get; private set; }
        public string DeviceName { get; private set; }
        public string ControlScheme { get; private set; }

        public delegate void NewFrameHandler(TickInput tick);

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

        //Accumulator for inputs. Checks if an input was registered between the fixed 60HZ ticks.

        private Vector2 _latchedDirection;

        public PlayerInputProvider(PlayerInput playerInput)
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

        public EInputProviderType ProviderType => EInputProviderType.Player;
        public InputBuffer Buffer { get; } = new();

        public TickInput UpdateFrameInput()
        {
            int direction = InputUtils.VectorToNumpad(_latchedDirection);
            if (direction == 0)
                direction = InputUtils.VectorToNumpad(_directionalInputAction.ReadValue<Vector2>());

            var currentDirection = InputUtils.NumpadToInputType(direction);
            var previousDirection = Buffer.GetFrame(0).Direction.Current;

            TickInput currentTick = new TickInput
            {
                Direction = new DirectionState
                {
                    Current = currentDirection,
                    Previous = previousDirection
                },
                LightAttack = _lightAttackButtonTracker.GetStateAndStep(),
                MediumAttack = _mediumAttackButtonTracker.GetStateAndStep(),
                HeavyAttack = _heavyAttackButtonTracker.GetStateAndStep(),
                UniqueAttack = _uniqueAttackButtonTracker.GetStateAndStep(),
                GuardButton = _guardButtonTracker.GetStateAndStep(),
                AbilityButton = _abilityButtonTracker.GetStateAndStep()
            };

            Buffer.Write(currentTick);
            _latchedDirection = Vector2.zero;
            return currentTick;
        }

        public void Flush()
        {
            _lightAttackButtonTracker.Reset();
            _mediumAttackButtonTracker.Reset();
            _heavyAttackButtonTracker.Reset();
            _uniqueAttackButtonTracker.Reset();
            _guardButtonTracker.Reset();
            _abilityButtonTracker.Reset();
            _latchedDirection = Vector2.zero;
        }
    }
}