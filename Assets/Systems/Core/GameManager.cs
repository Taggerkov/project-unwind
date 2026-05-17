using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using KinematicCharacterController;
using Systems.AsyncLoading;
using Systems.Combat;
using Systems.Combat.Combatant.Data;
using Systems.Common;
using Systems.Input;
using Systems.UI.CombatantSelect;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Systems.Core
{
    public enum GameState
    {
        MainMenu,
        CharacterSelect,
        Combat
    }

    public class GameManager : IDisposable
    {
        private readonly KCCSettings _kccSettings;

        private readonly CombatantSelectManager _combatantSelectManager;
        private readonly CombatManager _combatManager;

        private readonly PlayerRegistry _playerRegistry;

        private readonly AsyncLoader _asyncLoader;

        public event Action<GameState> OnGameStateChanged;

        public GameState CurrentGameState { get; private set; }


        public GameManager(KCCSettings kccSettings, CombatantSelectManager combatantSelectManager,
            CombatManager combatManager,
            PlayerRegistry playerRegistry, AsyncLoader asyncLoader)
        {
            _kccSettings = kccSettings;
            _combatantSelectManager = combatantSelectManager;
            _combatManager = combatManager;
            _playerRegistry = playerRegistry;
            _asyncLoader = asyncLoader;

            Debug.Log(UnityEditor.AssetDatabase.AssetPathToGUID("Assets/Systems/Audio/Test/AE_MetalSwingT.asset"));
            KinematicCharacterSystem.Settings = _kccSettings;
        }

        public void Dispose()
        {
            // _characterSelectManager.OnEncounterReady -= HandleEncounterReady;
            Debug.Log("GameManager: Dispose()");
        }

        private async void HandleEncounterReady(CombatEncounterData combatEncounterData)
        {
            try
            {
                _combatantSelectManager.OnEncounterReady -= HandleEncounterReady;

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

                var (stageData, combatantTuple) = await UniTask.WhenAll(
                    _asyncLoader.LoadStageData(combatEncounterData),
                    _asyncLoader.LoadCombatantData(combatEncounterData)
                );

                var (p0Data, p1Data) = combatantTuple;

                var sceneInstance = await _asyncLoader.LoadBattleAssets(stageData, p0Data, p1Data);

                Debug.Log("Loading Completed!");

                await BeginCombat(sceneInstance, p0Data, p1Data, combatant0InputProvider, combatant1InputProvider);
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
            CurrentGameState = GameState.CharacterSelect;
            OnGameStateChanged?.Invoke(CurrentGameState);

            _combatantSelectManager.Begin();
            _combatantSelectManager.OnEncounterReady += HandleEncounterReady;
        }

        public async UniTask BeginCombat(SceneInstance sceneInstance, CombatantDataSO combatant0Data,
            CombatantDataSO combatant1Data,
            IInputProvider combatant0InputProvider, IInputProvider combatant1InputProvider)
        {
            await _combatManager.PrepareCombat(sceneInstance, combatant0Data, combatant0InputProvider, combatant1Data,
                combatant1InputProvider);


            CurrentGameState = GameState.Combat;
            OnGameStateChanged?.Invoke(CurrentGameState);

            _combatManager.StartCombat();
        }
    }
}