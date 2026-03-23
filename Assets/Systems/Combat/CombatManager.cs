using FixedLogic;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.Combatant.Data;
using Systems.Core;
using Systems.Input;
using UnityEngine;


using _kccSystem = KinematicCharacterController.KinematicCharacterSystem;


namespace Systems.Combat
{
    public class CombatManager : ITickable
    {
        private CombatantBehaviour _combatant0;
        private CombatantBehaviour _combatant1;


        private bool _combatInProgress;

        public void SetCombatants(CombatantDataSO combatant0Data, CombatantDataSO combatant1Data)
        {
            // _combatant0 = Object.Instantiate(combatant0Data.combatantPrefab).GetComponent<CombatantBehaviour>();
            // _combatant1 = Object.Instantiate(combatant1Data.combatantPrefab).GetComponent<CombatantBehaviour>();
        }
        
        public void SetInputProviders(IInputProvider combatant0InputProvider, IInputProvider combatant1InputProvider)
        {
            _combatant0.InputProvider = combatant0InputProvider;
            _combatant1.InputProvider = combatant1InputProvider;
        }

        public void StartCombat()
        {
            string str = "Starting combat...\n";
            str += $"Combatant 0: {_combatant0.name} ProviderType: {_combatant0.InputProvider?.ProviderType.ToString() ?? "Null"}\n";
            str += $"Combatant 1: {_combatant1.name} ProviderType: {_combatant1.InputProvider?.ProviderType.ToString() ?? "Null"}\n";
            Debug.Log(str);

            _combatInProgress = true;
        }

        public void LogicTick()
        {
            
            Debug.Log("CombatManager LogicTick");
            
            if (!_combatInProgress) return;

            _kccSystem.Simulate(TickManager.TickInterval, _kccSystem.CharacterMotors, _kccSystem.PhysicsMovers);
        }
    }
}