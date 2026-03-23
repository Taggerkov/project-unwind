using FixedLogic;
using JetBrains.Annotations;
using KinematicCharacterController;
using Systems.Combat.Combatant.Controller;
using Systems.Input;
using UnityEngine;

namespace Systems.Combat.Combatant.Behaviour
{
    public class CombatantBehaviour : MonoBehaviour, ITickable
    {
        [SerializeField] private KinematicCharacterMotor motor;
        private CombatantCharacterController _characterController;
        
        [CanBeNull] public IInputProvider InputProvider { get; set; }

        private void Awake()
        {
            _characterController = new CombatantCharacterController();
            motor.CharacterController = _characterController;
        }
        
        public void InputTick()
        {
            if (InputProvider == null) return;
        }
    }
}
