using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using KinematicCharacterController;
using Systems.Audio;
using Systems.Audio.Music;
using Systems.Combat;
using Systems.Common;
using Systems.Core.ResourceManagement;
using Systems.CPU;
using Systems.Input;
using Systems.UI.CombatantSelect;
using Systems.UI.MainMenu;
using Systems.UI.Transition;
using UnityEngine;

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
        private readonly AudioManager _audioManager;
        private readonly MusicManager _musicManager;
        private readonly KCCSettings _kccSettings;

        private readonly MainMenuManager _mainMenuManager;
        private readonly CombatantSelectManager _combatantSelectManager;
        private readonly CombatManager _combatManager;

        private readonly PlayerRegistry _playerRegistry;

        private TransitionManager _transitionManager;

        private CombatSession _combatSession;

        public AudioManager AudioManager => _audioManager;

        public event Action<GameState> OnGameStateChanged;

        public GameState CurrentGameState { get; private set; }


        public GameManager(AudioManager audioManager, MusicManager musicManager, KCCSettings kccSettings,
            MainMenuCanvas mainMenuCanvas, CombatantSelectManager combatantSelectManager, CombatManager combatManager,
            PlayerRegistry playerRegistry, TransitionOverlay transitionOverlay)
        {
            _audioManager = audioManager;
            _musicManager = musicManager;
            _kccSettings = kccSettings;
            _combatantSelectManager = combatantSelectManager;
            _combatManager = combatManager;
            _playerRegistry = playerRegistry;

            _mainMenuManager = new MainMenuManager(mainMenuCanvas, playerRegistry);

            _transitionManager = new TransitionManager(transitionOverlay);

            KinematicCharacterSystem.Settings = _kccSettings;

            BeginMainMenu().Forget();
        }

        public void Dispose()
        {
            _mainMenuManager.Dispose();
            Debug.Log("GameManager: Dispose()");
        }

        private async void HandleEncounterReady(CombatEncounterData encounterData)
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
                        Debug.LogWarning("Only one player registered! Combatant 1 will be CPU driven.");
                        combatant0InputProvider = linkerList[0].PlayerInputProvider;
                        break;
                    default:
                        combatant0InputProvider = linkerList[0].PlayerInputProvider;
                        combatant1InputProvider = linkerList[1].PlayerInputProvider;
                        break;
                }

                await BeginCombat(encounterData, combatant0InputProvider, combatant1InputProvider);
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

        public async UniTask BeginMainMenu()
        {
            await _transitionManager.BeginLoading();

            CurrentGameState = GameState.MainMenu;
            OnGameStateChanged?.Invoke(CurrentGameState);

            _musicManager.ActivatePlaylist(PlaylistType.Menu).Forget();

            _mainMenuManager.OnPlayRequested -= HandlePlayRequested;
            _mainMenuManager.OnPlayRequested += HandlePlayRequested;
            _mainMenuManager.OnQuitRequested -= HandleQuitRequested;
            _mainMenuManager.OnQuitRequested += HandleQuitRequested;
            _mainMenuManager.Begin();

            _transitionManager.EndLoading();
        }

        private async void HandlePlayRequested()
        {
            UnsubscribeMainMenu();

            await _transitionManager.BeginLoading(); // dip to black before hiding the menu
            _mainMenuManager.End();

            await BeginCharacterSelect();

            _transitionManager.EndLoading();
        }

        private void HandleQuitRequested()
        {
            UnsubscribeMainMenu();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            WebInterop.RedirectSameTab("https://github.com/Taggerkov/project-unwind");
#else
            Application.Quit();
#endif
        }

        private void UnsubscribeMainMenu()
        {
            _mainMenuManager.OnPlayRequested -= HandlePlayRequested;
            _mainMenuManager.OnQuitRequested -= HandleQuitRequested;
        }

        public async UniTask BeginCharacterSelect()
        {
            await _transitionManager.BeginLoading();

            CurrentGameState = GameState.CharacterSelect;
            OnGameStateChanged?.Invoke(CurrentGameState);

            _musicManager.ActivatePlaylist(PlaylistType.Menu).Forget();
            _combatantSelectManager.Begin();
            _combatantSelectManager.OnEncounterReady += HandleEncounterReady;

            _transitionManager.EndLoading();
        }

        public async UniTask BeginCombat(CombatEncounterData encounterData,
            IInputProvider combatant0InputProvider, IInputProvider combatant1InputProvider)
        {
            await _transitionManager.BeginLoading(); // screen goes black first

            _combatSession = await CombatSession.LoadAsync(encounterData,
                onProgress: p => Debug.Log($"Loading: {p:P0}"));

            await _combatManager.PrepareCombat(_combatSession, combatant0InputProvider, combatant1InputProvider);

            CurrentGameState = GameState.Combat;
            OnGameStateChanged?.Invoke(CurrentGameState);
            _combatManager.OnCombatEnded += HandleCombatEnded;

            _musicManager.ActivatePlaylist(PlaylistType.Combat).Forget();
            _combatManager.StartCombat();

            _transitionManager.EndLoading();
        }

        private async void HandleCombatEnded()
        {
            _combatManager.OnCombatEnded -= HandleCombatEnded;

            await _transitionManager.BeginLoading();

            _combatManager.Cleanup();
            await _combatSession.DisposeAsync();
            _combatSession = null;

            await BeginMainMenu();

            _transitionManager.EndLoading();
        }
    }
}