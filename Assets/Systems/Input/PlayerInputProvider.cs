using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems.Input
{
    /// <summary>
    /// Bridges a single <see cref="InputAction"/> to a three-state <see cref="ButtonState"/> sampled
    /// at 60 Hz tick boundaries. Latches presses that arrive between ticks so no input is lost.
    /// </summary>
    public class ButtonTracker
    {
        /// <summary>True while the physical button is held according to the Input System canceled callback.</summary>
        private bool _physicalDown;

        /// <summary>
        /// Latched press flag; set on action.started and cleared after the next <see cref="GetStateAndStep"/>.
        /// Ensures a press that lands between ticks is still seen as Pressed on the next tick read.
        /// </summary>
        private bool _latchedPress;

        /// <summary>The Held state computed during the previous <see cref="GetStateAndStep"/> call.</summary>
        private bool _lastFrameHeld;

        /// <summary>
        /// Subscribes to the started and canceled phases of <paramref name="action"/> to track
        /// physical hold state and latch inter-tick presses.
        /// </summary>
        /// <param name="action">The input action to monitor.</param>
        public void LinkAction(InputAction action)
        {
            action.started += ctx =>
            {
                _physicalDown = true;
                _latchedPress = true;
            };
            action.canceled += ctx => _physicalDown = false;
        }

        /// <summary>
        /// Returns the current <see cref="ButtonState"/> derived from physical and latched state,
        /// then advances the tracker by clearing the latch and snapping <c>_lastFrameHeld</c>.
        /// </summary>
        /// <returns>The button state for the current tick.</returns>
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

    /// <summary>
    /// Translates Unity's New Input System events into the game's fixed-rate <see cref="TickInput"/>
    /// format, accumulating directional and button inputs between 60 Hz ticks so no input is lost.
    /// </summary>
    public class PlayerInputProvider : IInputProvider
    {
        /// <summary>Zero-based index of the player this provider represents.</summary>
        public int PlayerId { get; private set; }

        /// <summary>Display name of the physical device driving this provider.</summary>
        public string DeviceName { get; private set; }

        /// <summary>Name of the active control scheme (e.g. "Gamepad", "Keyboard").</summary>
        public string ControlScheme { get; private set; }

        /// <summary>Delegate type for the <see cref="OnNewFrame"/> event.</summary>
        public delegate void NewFrameHandler(TickInput tick);

        /// <summary>Raised each time <see cref="UpdateFrameInput"/> writes a new tick to the buffer.</summary>
        public event NewFrameHandler OnNewFrame;

        /// <summary>The Unity PlayerInput component this provider reads from.</summary>
        private PlayerInput _pi;

        /// <summary>Cached directional axis action resolved from the player's action asset.</summary>
        private InputAction _directionalInputAction;

        /// <summary>Cached action for the light attack button.</summary>
        private InputAction _lightAttackAction;

        /// <summary>Cached action for the medium attack button.</summary>
        private InputAction _mediumAttackAction;

        /// <summary>Cached action for the heavy attack button.</summary>
        private InputAction _heavyAttackAction;

        /// <summary>Cached action for the unique attack button.</summary>
        private InputAction _uniqueAttackAction;

        /// <summary>Cached action for the guard button.</summary>
        private InputAction _guardButtonAction;

        /// <summary>Cached action for the ability button.</summary>
        private InputAction _abilityButtonAction;

        /// <summary>Tick-boundary sampler for the light attack button.</summary>
        private ButtonTracker _lightAttackButtonTracker;

        /// <summary>Tick-boundary sampler for the medium attack button.</summary>
        private ButtonTracker _mediumAttackButtonTracker;

        /// <summary>Tick-boundary sampler for the heavy attack button.</summary>
        private ButtonTracker _heavyAttackButtonTracker;

        /// <summary>Tick-boundary sampler for the unique attack button.</summary>
        private ButtonTracker _uniqueAttackButtonTracker;

        /// <summary>Tick-boundary sampler for the guard button.</summary>
        private ButtonTracker _guardButtonTracker;

        /// <summary>Tick-boundary sampler for the ability button.</summary>
        private ButtonTracker _abilityButtonTracker;

        /// <summary>
        /// Accumulated directional input between ticks; latches the strongest value seen since
        /// the last <see cref="UpdateFrameInput"/> call and is reset to zero after each read.
        /// </summary>
        private Vector2 _latchedDirection;

        /// <summary>
        /// Caches all input actions from <paramref name="playerInput"/>, wires button trackers
        /// to their actions, and subscribes to directional input to latch peak values between ticks.
        /// </summary>
        /// <param name="playerInput">The Unity PlayerInput component for this player.</param>
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

        /// <inheritdoc/>
        public EInputProviderType ProviderType => EInputProviderType.Player;

        /// <inheritdoc/>
        public InputBuffer Buffer { get; } = new();

        /// <summary>
        /// Samples all button trackers and the latched directional input, writes the resulting
        /// <see cref="TickInput"/> to the buffer, resets the direction latch, and returns the tick.
        /// </summary>
        /// <returns>The <see cref="TickInput"/> written to the buffer for this tick.</returns>
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