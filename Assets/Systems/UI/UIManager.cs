using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Systems.Audio;
using Systems.Core;
using Systems.Input;
using Systems.UI.Contracts;
using Systems.UI.Core;
using Systems.UI.Core.Transition;
using UnityEngine.UI;

namespace Systems.UI
{
    /// <summary>
    /// The single owner of all menu UI infrastructure. Tracks which controllers are attached to the
    /// active screen, routes their navigate and submit input, switches their action maps, drives the
    /// cursor through <see cref="CursorController"/>, plays UI sounds, and runs the fade between
    /// screens. Screens stay free of this plumbing: <see cref="GameManager"/> decides which screen to
    /// show and the manager wires everything else. Reconnect is handled here once, with per-player
    /// selection held by the cursor so a returning controller resumes where it left off.
    /// </summary>
    public sealed class UIManager : IUIContext, IDisposable
    {
        /// <summary>Action map a controller uses while driving a menu.</summary>
        private const string UIActionMap = "UI";

        /// <summary>Action map a controller reverts to once it leaves a menu or locks in a choice.</summary>
        private const string GameActionMap = "Game";

        /// <summary>Source of controller join and leave notifications; subscribed in the constructor.</summary>
        private readonly PlayerRegistry _playerRegistry;

        /// <summary>Used to play the navigate and confirm sounds on every interaction.</summary>
        private readonly AudioManager _audioManager;

        /// <summary>Holds the configured sound events and player cursor colours.</summary>
        private readonly UISettings _settings;

        /// <summary>Drives the fade overlay between screens.</summary>
        private readonly TransitionManager _transition;

        /// <summary>Owns cursor objects and selection state for the active screen.</summary>
        private readonly CursorController _cursor;

        /// <summary>Controllers currently attached to the active screen, in join order.</summary>
        private readonly List<PlayerLinker> _linkers = new();

        /// <summary>The screen currently displayed, or null when no screen is active.</summary>
        private IUIScreen _activeScreen;

        /// <summary>
        /// Preload task started eagerly in the constructor and awaited in <see cref="Show"/> before
        /// the screen becomes interactive. Using <c>.Preserve()</c> allows multiple Show calls to
        /// await the same completed task without throwing.
        /// </summary>
        private UniTask _soundsPreloadTask;

        /// <summary>
        /// Builds the transition and cursor services, subscribes to controller join and leave, and
        /// preloads the UI sounds so the first navigation is not silent.
        /// </summary>
        /// <param name="playerRegistry">Source of controller join and leave notifications.</param>
        /// <param name="audioManager">Used to play navigate and confirm sounds.</param>
        /// <param name="settings">Holds the navigate and confirm sound events.</param>
        /// <param name="transitionOverlay">The fade overlay driven between screens.</param>
        public UIManager(PlayerRegistry playerRegistry, AudioManager audioManager, UISettings settings,
            TransitionOverlay transitionOverlay)
        {
            _playerRegistry = playerRegistry ?? throw new ArgumentNullException(nameof(playerRegistry));
            _audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _transition = new TransitionManager(transitionOverlay);
            _cursor = new CursorController(_settings.Player0Colour, _settings.Player1Colour);

            _playerRegistry.OnPlayerJoined += AttachLinker;
            _playerRegistry.OnPlayerLeft += DetachLinker;

            _soundsPreloadTask = PreloadSoundsAsync().Preserve();
        }

        /// <summary>Detaches every controller, releases the active screen, and unsubscribes from the registry.</summary>
        public void Dispose()
        {
            _playerRegistry.OnPlayerJoined -= AttachLinker;
            _playerRegistry.OnPlayerLeft -= DetachLinker;
            ExitCurrent();
        }

        /// <summary>Controllers currently attached to the active screen, in join order.</summary>
        public IReadOnlyList<PlayerLinker> ActiveLinkers => _linkers;

        /// <summary>
        /// Fades to black, swaps to <paramref name="screen"/>, attaches every connected controller, then
        /// fades back in. The previous screen is fully torn down first.
        /// </summary>
        /// <param name="screen">The screen to show.</param>
        public async UniTask Show(IUIScreen screen)
        {
            await _transition.BeginLoading();
            await _soundsPreloadTask;

            ExitCurrent();

            _activeScreen = screen;
            _cursor.Configure(screen.CursorMode, screen.CursorParent);
            screen.Enter(this);

            foreach (var linker in _playerRegistry.GetAllPlayers())
                AttachLinker(linker);

            _transition.EndLoading();
        }

        /// <summary>
        /// Tears down the active screen without a fade: detaches every controller, reverting each to the
        /// gameplay action map, exits the screen and clears the cursor. Used when entering combat, where
        /// the caller owns the surrounding fade.
        /// </summary>
        public void ExitCurrent()
        {
            if (_activeScreen == null) return;

            foreach (var linker in _linkers.ToArray())
                DetachLinker(linker);

            _activeScreen.Exit();
            _cursor.Clear();
            _activeScreen = null;
        }

        /// <summary>Fades the screen to black. Await before loading resources that should be hidden.</summary>
        public UniTask BeginLoading() => _transition.BeginLoading();

        /// <summary>Fades the screen back in.</summary>
        public void EndLoading() => _transition.EndLoading();

        /// <inheritdoc/>
        public void SetSharedSelection(Selectable selectable) => _cursor.SetSharedSelection(selectable);

        /// <inheritdoc/>
        public void SetSelection(int playerId, Selectable selectable) => _cursor.SetSelection(playerId, selectable);

        /// <inheritdoc/>
        public void SetPlayerEnabled(int playerId, bool enabled)
        {
            var linker = LinkerFor(playerId);
            if (linker) linker.PlayerInput.SwitchCurrentActionMap(enabled ? UIActionMap : GameActionMap);
            _cursor.SetPlayerEnabled(playerId, enabled);
        }

        /// <inheritdoc/>
        public void RefreshCursors() => _cursor.Refresh();

        /// <summary>
        /// Attaches a controller to the active screen: subscribes its input, switches its action map
        /// (unless the player is held disabled from before a reconnect), focuses its held or default
        /// selectable, and notifies the screen.
        /// </summary>
        /// <param name="linker">The controller to attach.</param>
        private void AttachLinker(PlayerLinker linker)
        {
            if (_activeScreen == null || !linker || _linkers.Contains(linker)) return;

            _linkers.Add(linker);
            linker.OnUISubmit += HandleSubmit;
            linker.OnUINavigate += HandleNavigate;

            bool enabled = _cursor.IsPlayerEnabled(linker.PlayerId);
            linker.PlayerInput.SwitchCurrentActionMap(enabled ? UIActionMap : GameActionMap);

            var held = _cursor.GetSelection(linker.PlayerId);
            var focus = held ? held : _activeScreen.GetDefaultSelectable(linker.PlayerId);
            _cursor.AttachPlayer(linker, focus);

            _activeScreen.OnPlayerAttached(linker);
        }

        /// <summary>
        /// Detaches a controller: unsubscribes its input, reverts it to the gameplay action map, drops
        /// its cursor (held state is preserved for reconnect), and notifies the screen.
        /// </summary>
        /// <param name="linker">The controller to detach.</param>
        private void DetachLinker(PlayerLinker linker)
        {
            if (!_linkers.Remove(linker)) return;

            linker.OnUISubmit -= HandleSubmit;
            linker.OnUINavigate -= HandleNavigate;

            if (linker) linker.PlayerInput.SwitchCurrentActionMap(GameActionMap);
            _cursor.DetachPlayer(linker);

            _activeScreen?.OnPlayerDetached(linker);
        }

        /// <summary>Updates the cursor, plays the navigate sound, and forwards navigation to the active screen.</summary>
        private void HandleNavigate(PlayerLinker linker, Selectable previous, Selectable current)
        {
            _cursor.HandleNavigate(linker, current);
            if (_settings.NavigateSound) _audioManager.Play(_settings.NavigateSound);
            _activeScreen?.OnNavigate(linker, previous, current);
        }

        /// <summary>Plays the confirm sound and forwards the submit to the active screen.</summary>
        private void HandleSubmit(PlayerLinker linker, Selectable selectable)
        {
            if (_settings.ConfirmSound) _audioManager.Play(_settings.ConfirmSound);
            _activeScreen?.OnSubmit(linker, selectable);
        }

        /// <summary>Returns the attached controller for a player id, or null.</summary>
        private PlayerLinker LinkerFor(int playerId) => _linkers.FirstOrDefault(l => l && l.PlayerId == playerId);

        /// <summary>
        /// Preloads the navigate and confirm sounds via the audio manager, skipping any
        /// unset entries. The result is stored in <see cref="_soundsPreloadTask"/> and awaited
        /// in <see cref="Show"/> so the first interaction is never silent.
        /// </summary>
        private async UniTask PreloadSoundsAsync()
        {
            var sounds = new[] { _settings.NavigateSound, _settings.ConfirmSound }.Where(s => s);
            await _audioManager.PreloadAsync(sounds);
        }
    }
}
