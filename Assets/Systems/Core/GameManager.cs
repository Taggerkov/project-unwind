using System;
using System.Collections.Generic;
using KinematicCharacterController;
using Reflex.Attributes;
using Systems.AsyncLoading;
using Systems.Combat;
using Systems.Common;
using Systems.Input;
using UnityEngine;

namespace Systems.Core
{
    public class GameManager : IDisposable
    {
        private readonly KCCSettings _kccSettings;

        private readonly CharacterSelectManager _characterSelectManager;
        private readonly CombatManager _combatManager;

        private readonly PlayerRegistry _playerRegistry;

        private readonly AsyncLoader _asyncLoader;

        public GameManager(KCCSettings kccSettings, CharacterSelectManager characterSelectManager, CombatManager combatManager,
            PlayerRegistry playerRegistry, AsyncLoader asyncLoader)
        {
            _kccSettings = kccSettings;
            _characterSelectManager = characterSelectManager;
            _combatManager = combatManager;
            _playerRegistry = playerRegistry;
            _asyncLoader = asyncLoader;
            
            KinematicCharacterSystem.Settings = _kccSettings;
            _characterSelectManager.OnEncounterReady += HandleEncounterReady;
        }

        public void Dispose()
        {
            _characterSelectManager.OnEncounterReady -= HandleEncounterReady;
            Debug.Log("GameManager: Dispose()");
        }

        private async void HandleEncounterReady(CombatEncounterData combatEncounterData)
        {
            try
            {
                var linkerList = GetAllPlayers();

                IInputProvider combatant0InputProvider = null;
                IInputProvider combatant1InputProvider = null;

                switch (linkerList.Count)
                {
                    case 0:
                        Debug.LogWarning("No players registered! Combatants will have no input providers.");
                        break;
                    case 1:
                        Debug.LogWarning("Only one player registered! Combatant 1 will have no input provider.");
                        combatant0InputProvider = linkerList[0].PlayerInputProvider;
                        break;
                    default:
                        combatant0InputProvider = linkerList[0].PlayerInputProvider;
                        combatant1InputProvider = linkerList[1].PlayerInputProvider;
                        break;
                }

                Debug.Log("Loading Started...");

                var (p0Data, p1Data) = await _asyncLoader.LoadCombatantData(combatEncounterData);

                await _asyncLoader.LoadBattleAssets(combatEncounterData.Stage, p0Data, p1Data);

                Debug.Log("Loading Completed!");


                _combatManager.SetCombatants(p0Data, p1Data);
                _combatManager.SetInputProviders(combatant0InputProvider, combatant1InputProvider);
                _combatManager.StartCombat();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during encounter setup: {e}");
            }
        }

        private List<PlayerLinker> GetAllPlayers()
        {
            return _playerRegistry.GetAllPlayers();
        }

        public void BeginCharacterSelect()
        {
            _characterSelectManager.BeginCharacterSelection();
        }
    }
}