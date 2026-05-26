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
using Systems.UI;
using Systems.UI.Menu.CombatantSelect;
using Systems.UI.Menu.MainMenu;
using UnityEngine;

namespace Systems.Core
{
    /// <summary>Top-level game phase, driven by <see cref="GameManager"/>.</summary>
    public enum GameState
    {
        /// <summary>The title screen is active.</summary>
        MainMenu,

        /// <summary>The combatant and stage selection screen is active.</summary>
        CharacterSelect,

        /// <summary>A combat round is in progress.</summary>
        Combat
    }

    /// <summary>
    /// Top-level state machine that transitions between main menu, character select, and combat.
    /// Owns the <see cref="CombatSession"/> lifetime and orchestrates audio playlist switches at
    /// each state boundary. All navigation is driven by events from <see cref="UIManager"/> screens.
    /// </summary>
    public class GameManager : IDisposable
    {
        /// <summary>Used to pass to callers that need a direct audio reference (e.g. UI sound triggers).</summary>
        private readonly AudioManager _audioManager;

        /// <summary>Controls which music playlist is active at each game state.</summary>
        private readonly MusicManager _musicManager;

        /// <summary>KCC physics settings applied globally at construction.</summary>
        private readonly KCCSettings _kccSettings;

        /// <summary>Drives the active menu screen and the fade transition between them.</summary>
        private readonly UIManager _uiManager;

        /// <summary>The main menu screen instance shown at game start and after combat.</summary>
        private readonly MainMenuScreen _mainMenuScreen;

        /// <summary>The character and stage selection screen.</summary>
        private readonly CombatantSelectScreen _characterSelectScreen;

        /// <summary>Runs the combat simulation and exposes its lifecycle events.</summary>
        private readonly CombatManager _combatManager;

        /// <summary>Source of player join and leave notifications used when assigning input providers.</summary>
        private readonly PlayerRegistry _playerRegistry;

        /// <summary>Active combat session; non-null while in the Combat state, null otherwise.</summary>
        private CombatSession _combatSession;

        /// <summary>Exposes the audio manager for systems that require a direct reference.</summary>
        public AudioManager AudioManager => _audioManager;

        /// <summary>Raised whenever <see cref="CurrentGameState"/> changes.</summary>
        public event Action<GameState> OnGameStateChanged;

        /// <summary>The phase the game is currently in.</summary>
        public GameState CurrentGameState { get; private set; }

        /// <summary>
        /// Applies KCC settings globally, then starts the main menu flow.
        /// </summary>
        public GameManager(AudioManager audioManager, MusicManager musicManager, KCCSettings kccSettings,
            UIManager uiManager, MainMenuScreen mainMenuScreen, CombatantSelectScreen characterSelectScreen,
            CombatManager combatManager, PlayerRegistry playerRegistry)
        {
            _audioManager = audioManager;
            _musicManager = musicManager;
            _kccSettings = kccSettings;
            _uiManager = uiManager;
            _mainMenuScreen = mainMenuScreen;
            _characterSelectScreen = characterSelectScreen;
            _combatManager = combatManager;
            _playerRegistry = playerRegistry;

            KinematicCharacterSystem.Settings = _kccSettings;

            BeginMainMenu().Forget();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Debug.Log("GameManager: Dispose()");
        }

        /// <summary>
        /// Called when the character select screen signals that a valid encounter is ready.
        /// Assigns input providers from connected players (CPU fills any missing slot) and
        /// transitions to combat.
        /// </summary>
        private async void HandleEncounterReady(CombatEncounterData encounterData)
        {
            try
            {
                _characterSelectScreen.OnEncounterReady -= HandleEncounterReady;

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

        /// <summary>Returns all currently joined players from the registry.</summary>
        private List<PlayerLinker> GetAllPlayers()
        {
            return _playerRegistry.GetAllPlayers();
        }

        /// <summary>
        /// Transitions to the main menu: activates the menu playlist, wires screen events,
        /// and shows the main menu screen via a fade.
        /// </summary>
        public async UniTask BeginMainMenu()
        {
            CurrentGameState = GameState.MainMenu;
            OnGameStateChanged?.Invoke(CurrentGameState);

            _musicManager.ActivatePlaylist(PlaylistType.Menu).Forget();

            _mainMenuScreen.OnPlayRequested -= HandlePlayRequested;
            _mainMenuScreen.OnPlayRequested += HandlePlayRequested;
            _mainMenuScreen.OnQuitRequested -= HandleQuitRequested;
            _mainMenuScreen.OnQuitRequested += HandleQuitRequested;

            await _uiManager.Show(_mainMenuScreen);
        }

        /// <summary>Unsubscribes main menu events and transitions to character select.</summary>
        private async void HandlePlayRequested()
        {
            UnsubscribeMainMenu();
            await BeginCharacterSelect();
        }

        /// <summary>
        /// Exits play mode in the Editor, redirects to the project page on WebGL, or
        /// calls <c>Application.Quit</c> on standalone builds.
        /// </summary>
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

        /// <summary>Removes both main menu event subscriptions.</summary>
        private void UnsubscribeMainMenu()
        {
            _mainMenuScreen.OnPlayRequested -= HandlePlayRequested;
            _mainMenuScreen.OnQuitRequested -= HandleQuitRequested;
        }

        /// <summary>
        /// Transitions to character select: activates the menu playlist, wires the encounter-ready
        /// event, and shows the character select screen via a fade.
        /// </summary>
        public async UniTask BeginCharacterSelect()
        {
            CurrentGameState = GameState.CharacterSelect;
            OnGameStateChanged?.Invoke(CurrentGameState);

            _musicManager.ActivatePlaylist(PlaylistType.Menu).Forget();

            _characterSelectScreen.OnEncounterReady -= HandleEncounterReady;
            _characterSelectScreen.OnEncounterReady += HandleEncounterReady;

            await _uiManager.Show(_characterSelectScreen);
        }

        /// <summary>
        /// Fades to black, exits the current screen, loads and activates the combat session,
        /// starts the combat playlist and the combat simulation, then fades back in.
        /// </summary>
        /// <param name="encounterData">Addressable references for the two combatants and the stage.</param>
        /// <param name="combatant0InputProvider">Input provider for the first combatant; may be null.</param>
        /// <param name="combatant1InputProvider">Input provider for the second combatant; may be null.</param>
        public async UniTask BeginCombat(CombatEncounterData encounterData,
            IInputProvider combatant0InputProvider, IInputProvider combatant1InputProvider)
        {
            await _uiManager.BeginLoading();
            _uiManager.ExitCurrent();

            _combatSession = await CombatSession.LoadAsync(encounterData,
                onProgress: p => Debug.Log($"Loading: {p:P0}"));

            await _combatManager.PrepareCombat(_combatSession, combatant0InputProvider, combatant1InputProvider);

            CurrentGameState = GameState.Combat;
            OnGameStateChanged?.Invoke(CurrentGameState);
            _combatManager.OnCombatEnded += HandleCombatEnded;

            _musicManager.ActivatePlaylist(PlaylistType.Combat).Forget();
            _combatManager.StartCombat();

            _uiManager.EndLoading();
        }

        /// <summary>
        /// Called when combat ends: fades to black, cleans up the combat manager, disposes
        /// the session, and returns to the main menu.
        /// </summary>
        private async void HandleCombatEnded()
        {
            _combatManager.OnCombatEnded -= HandleCombatEnded;

            await _uiManager.BeginLoading();

            _combatManager.Cleanup();
            await _combatSession.DisposeAsync();
            _combatSession = null;

            await BeginMainMenu();
        }
    }
}