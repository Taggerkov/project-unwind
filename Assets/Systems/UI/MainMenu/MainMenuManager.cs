using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Core;
using Systems.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Systems.UI.MainMenu
{
    /// <summary>
    /// Drives the title screen. Any connected controller can navigate and confirm; they all share a
    /// single cursor, kept in sync by mirroring the selection onto every controller's event system.
    /// Identity does not matter here, as player slots are decided later by join order in character
    /// select. Exposes the player's high-level intent via events and owns the in-canvas Help panel.
    /// </summary>
    public class MainMenuManager : IDisposable
    {
        /// <summary>Raised when the player chooses to start the game.</summary>
        public event Action OnPlayRequested;

        /// <summary>Raised when the player chooses to quit the game.</summary>
        public event Action OnQuitRequested;

        /// <summary>Root canvas hosting the menu panels; toggled on by <see cref="Begin"/> and off by <see cref="End"/>.</summary>
        private readonly Canvas _canvas;

        /// <summary>Source of controller join and leave notifications.</summary>
        private readonly PlayerRegistry _playerRegistry;

        /// <summary>Panel holding the primary menu buttons (Play, Help, Quit).</summary>
        private readonly GameObject _mainPanel;

        /// <summary>Panel holding the scrollable Help content and its Back button.</summary>
        private readonly GameObject _helpPanel;

        /// <summary>Button on the Main panel that requests game start.</summary>
        private readonly Button _playButton;

        /// <summary>Button on the Main panel that opens the Help panel.</summary>
        private readonly Button _helpButton;

        /// <summary>Button on the Main panel that requests application quit.</summary>
        private readonly Button _quitButton;

        /// <summary>Button on the Help panel that returns to the Main panel.</summary>
        private readonly Button _helpBackButton;

        /// <summary>Drives scrolling of the Help content from every controller's scroll actions.</summary>
        private readonly HelpScrollController _helpScroll;

        /// <summary>Resources path of the Help panel text content.</summary>
        private const string HelpContentPath = "MainMenuHelp";

        /// <summary>
        /// Action driving Help "scroll up". Reuses the gameplay Light action (keyboard U, gamepad West),
        /// as those are the only buttons available on this controller layout.
        /// </summary>
        private const string ScrollUpActionName = "LightAttack";

        /// <summary>
        /// Action driving Help "scroll down". Reuses the gameplay Medium action (keyboard I, gamepad North),
        /// as those are the only buttons available on this controller layout.
        /// </summary>
        private const string ScrollDownActionName = "MediumAttack";

        /// <summary>Name of the action map a controller uses while on the menu.</summary>
        private const string UIActionMap = "UI";

        /// <summary>Name of the action map a controller reverts to once it leaves the menu.</summary>
        private const string GameActionMap = "Game";

        /// <summary>Every controller currently driving the shared menu cursor.</summary>
        private readonly List<PlayerLinker> _linkers = new();

        /// <summary>The panel currently shown; preserved across disconnects so a rejoin resumes here.</summary>
        private EMenuPanel _currentPanel = EMenuPanel.Main;

        /// <summary>The shared cursor's selectable, mirrored onto every controller's event system.</summary>
        private Selectable _lastSelected;

        /// <summary>Identifies which menu panel is active.</summary>
        private enum EMenuPanel
        {
            /// <summary>The primary panel with the Play, Help and Quit buttons.</summary>
            Main,

            /// <summary>The scrollable Help panel with its Back button.</summary>
            Help
        }

        #region Element names

        /// <summary>Child name of the Main panel under the canvas.</summary>
        private const string MainPanelName = "MainPanel";

        /// <summary>Child name of the Help panel under the canvas.</summary>
        private const string HelpPanelName = "HelpPanel";

        /// <summary>Child name of the Play button under the Main panel.</summary>
        private const string PlayButtonName = "PlayButton";

        /// <summary>Child name of the Help button under the Main panel.</summary>
        private const string HelpButtonName = "HelpButton";

        /// <summary>Child name of the Quit button under the Main panel.</summary>
        private const string QuitButtonName = "QuitButton";

        /// <summary>Child name of the Back button under the Help panel.</summary>
        private const string HelpBackButtonName = "BackButton";

        /// <summary>Child name of the scroll view under the Help panel.</summary>
        private const string HelpScrollName = "HelpScroll";

        #endregion

        /// <summary>
        /// Resolves every required canvas element by name and loads the Help text. Throws if any
        /// dependency or element is missing: the menu is mandatory, so a malformed canvas must fail
        /// loudly at construction rather than yield a half-built screen.
        /// </summary>
        /// <param name="canvas">Strongly typed wrapper around the menu's root canvas.</param>
        /// <param name="playerRegistry">Registry providing controller join and leave notifications.</param>
        /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
        /// <exception cref="InvalidOperationException">A required canvas element is missing or malformed.</exception>
        public MainMenuManager(MainMenuCanvas canvas, PlayerRegistry playerRegistry)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _playerRegistry = playerRegistry ?? throw new ArgumentNullException(nameof(playerRegistry));

            if (!TryFind(_canvas.transform, MainPanelName, out _mainPanel))
                throw new InvalidOperationException($"MainMenuManager: could not find '{MainPanelName}' in the canvas.");

            if (!TryFind(_canvas.transform, HelpPanelName, out _helpPanel))
                throw new InvalidOperationException($"MainMenuManager: could not find '{HelpPanelName}' in the canvas.");

            TryFind(_mainPanel.transform, PlayButtonName, out _playButton);
            TryFind(_mainPanel.transform, HelpButtonName, out _helpButton);
            TryFind(_mainPanel.transform, QuitButtonName, out _quitButton);
            if (!_playButton || !_helpButton || !_quitButton)
                throw new InvalidOperationException(
                    $"MainMenuManager: '{MainPanelName}' must contain Buttons named '{PlayButtonName}', '{HelpButtonName}' and '{QuitButtonName}'.");

            if (!TryFind(_helpPanel.transform, HelpBackButtonName, out _helpBackButton))
                throw new InvalidOperationException(
                    $"MainMenuManager: '{HelpPanelName}' must contain a Button named '{HelpBackButtonName}'.");

            if (!TryFind(_helpPanel.transform, HelpScrollName, out ScrollRect helpScroll))
                throw new InvalidOperationException(
                    $"MainMenuManager: '{HelpPanelName}' must contain a ScrollRect named '{HelpScrollName}'.");

            var helpText = helpScroll.content ? helpScroll.content.GetComponent<Text>() : null;
            if (!helpText)
                throw new InvalidOperationException($"MainMenuManager: '{HelpScrollName}' content must have a Text component.");

            var asset = Resources.Load<TextAsset>(HelpContentPath);
            if (asset)
                helpText.text = asset.text;
            else
                Debug.LogWarning($"MainMenuManager: Help content not found at Resources/{HelpContentPath}.txt.");

            _helpScroll = new HelpScrollController(helpScroll, ScrollUpActionName, ScrollDownActionName);
        }

        /// <summary>Detaches registry handlers and releases all controllers.</summary>
        public void Dispose()
        {
            _playerRegistry.OnPlayerJoined -= HandlePlayerJoined;
            _playerRegistry.OnPlayerLeft -= HandlePlayerLeft;
            TeardownLinkers();
        }

        /// <summary>
        /// Shows the menu on the Main panel, subscribes to controller join and leave events, and adds
        /// every already connected controller to the shared cursor.
        /// </summary>
        public void Begin()
        {
            _canvas.gameObject.SetActive(true);
            ShowPanel(EMenuPanel.Main);

            _playerRegistry.OnPlayerJoined -= HandlePlayerJoined;
            _playerRegistry.OnPlayerJoined += HandlePlayerJoined;
            _playerRegistry.OnPlayerLeft -= HandlePlayerLeft;
            _playerRegistry.OnPlayerLeft += HandlePlayerLeft;

            foreach (var linker in _playerRegistry.GetAllPlayers())
                AddLinker(linker);
        }

        /// <summary>
        /// Hides the menu and releases input. Call when leaving the main menu so the
        /// next screen starts from a clean state.
        /// </summary>
        public void End()
        {
            _playerRegistry.OnPlayerJoined -= HandlePlayerJoined;
            _playerRegistry.OnPlayerLeft -= HandlePlayerLeft;
            TeardownLinkers();

            _canvas.gameObject.SetActive(false);
            _helpPanel.SetActive(false);
            _mainPanel.SetActive(false);
        }

        /// <summary>Adds a newly joined controller to the shared cursor.</summary>
        /// <param name="playerLinker">The controller that just joined.</param>
        private void HandlePlayerJoined(PlayerLinker playerLinker) => AddLinker(playerLinker);

        /// <summary>Removes a controller that left from the shared cursor.</summary>
        /// <param name="playerLinker">The controller that just left.</param>
        private void HandlePlayerLeft(PlayerLinker playerLinker) => RemoveLinker(playerLinker);

        /// <summary>
        /// Switches a controller to the UI map, subscribes its submit and navigate events, places its
        /// cursor on the shared selection, and (on the Help panel) rebinds scrolling to include it.
        /// No-op if the controller is missing or already present.
        /// </summary>
        /// <param name="linker">The controller to add.</param>
        private void AddLinker(PlayerLinker linker)
        {
            if (!linker || _linkers.Contains(linker)) return;

            _linkers.Add(linker);
            Subscribe(linker);
            linker.PlayerInput.SwitchCurrentActionMap(UIActionMap);

            var focus = _lastSelected ? _lastSelected : DefaultSelectable(_currentPanel);
            if (focus) linker.MultiplayerEventSystem.SetSelectedGameObject(focus.gameObject);

            if (_currentPanel == EMenuPanel.Help) StartHelpScroll();
        }

        /// <summary>
        /// Removes a controller from the shared cursor: unsubscribes its events, clears its cursor,
        /// reverts it to the gameplay action map, and (on the Help panel) rebinds scrolling without it.
        /// </summary>
        /// <param name="linker">The controller to remove.</param>
        private void RemoveLinker(PlayerLinker linker)
        {
            if (!_linkers.Remove(linker)) return;

            Unsubscribe(linker);
            if (linker)
            {
                linker.MultiplayerEventSystem.SetSelectedGameObject(null);
                linker.PlayerInput.SwitchCurrentActionMap(GameActionMap);
            }

            if (_currentPanel == EMenuPanel.Help) StartHelpScroll();
        }

        /// <summary>Stops Help scrolling, unsubscribes and reverts every controller, and clears the list.</summary>
        private void TeardownLinkers()
        {
            _helpScroll.Stop();

            foreach (var linker in _linkers.ToArray())
            {
                Unsubscribe(linker);
                if (!linker) continue;
                linker.MultiplayerEventSystem.SetSelectedGameObject(null);
                linker.PlayerInput.SwitchCurrentActionMap(GameActionMap);
            }

            _linkers.Clear();
        }

        /// <summary>Subscribes the manager's handlers to a controller's UI events.</summary>
        /// <param name="linker">The controller to subscribe.</param>
        private void Subscribe(PlayerLinker linker)
        {
            linker.OnUISubmit += OnPlayerSubmit;
            linker.OnUINavigate += OnPlayerNavigate;
        }

        /// <summary>Unsubscribes the manager's handlers from a controller's UI events.</summary>
        /// <param name="linker">The controller to unsubscribe.</param>
        private void Unsubscribe(PlayerLinker linker)
        {
            linker.OnUISubmit -= OnPlayerSubmit;
            linker.OnUINavigate -= OnPlayerNavigate;
        }

        /// <summary>
        /// Handles a submit from any controller. Routes the selected button to the matching intent or
        /// panel change; the shared cursor means every controller submits the same selectable.
        /// </summary>
        /// <param name="linker">The controller that submitted.</param>
        /// <param name="selectable">The selectable that was active when submit fired.</param>
        private void OnPlayerSubmit(PlayerLinker linker, Selectable selectable)
        {
            if (selectable == _playButton)
                OnPlayRequested?.Invoke();
            else if (selectable == _helpButton)
                ShowPanel(EMenuPanel.Help);
            else if (selectable == _quitButton)
                OnQuitRequested?.Invoke();
            else if (selectable == _helpBackButton)
                ShowPanel(EMenuPanel.Main);
        }

        /// <summary>
        /// Handles navigation from any controller. The navigating controller's event system has already
        /// moved; mirror the new selection onto every other controller so the cursor stays unified.
        /// </summary>
        /// <param name="linker">The controller that navigated.</param>
        /// <param name="previous">The previously focused selectable.</param>
        /// <param name="current">The newly focused selectable.</param>
        private void OnPlayerNavigate(PlayerLinker linker, Selectable previous, Selectable current)
        {
            if (current) SetSelection(current);
        }

        /// <summary>
        /// Activates the requested panel, deactivates the other, moves the shared cursor to that panel's
        /// default selection, and binds or unbinds Help scrolling accordingly.
        /// </summary>
        /// <param name="panel">The panel to show.</param>
        private void ShowPanel(EMenuPanel panel)
        {
            _currentPanel = panel;
            switch (panel)
            {
                case EMenuPanel.Main:
                    _helpScroll.Stop();
                    _helpPanel.SetActive(false);
                    _mainPanel.SetActive(true);
                    Canvas.ForceUpdateCanvases();
                    SetSelection(_playButton);
                    break;

                case EMenuPanel.Help:
                    _mainPanel.SetActive(false);
                    _helpPanel.SetActive(true);
                    Canvas.ForceUpdateCanvases();
                    _helpScroll.ResetToTop();
                    SetSelection(_helpBackButton);
                    StartHelpScroll();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(panel), panel, null);
            }
        }

        /// <summary>Starts Help scrolling, fed by the scroll actions of every controller on the menu.</summary>
        private void StartHelpScroll()
        {
            var inputs = new List<PlayerInput>(_linkers.Count);
            inputs.AddRange(from linker in _linkers where linker && linker.PlayerInput select linker.PlayerInput);

            _helpScroll.Begin(inputs);
        }

        /// <summary>
        /// Records the shared selection and mirrors it onto every controller's event system, skipping
        /// any already pointing at it so a navigating controller is not redundantly reselected.
        /// </summary>
        /// <param name="selectable">The selectable to focus on every controller.</param>
        private void SetSelection(Selectable selectable)
        {
            _lastSelected = selectable;
            if (!selectable) return;

            var target = selectable.gameObject;
            foreach (var linker in _linkers.Where(linker => linker).Where(linker => linker.MultiplayerEventSystem.currentSelectedGameObject != target))
            {
                linker.MultiplayerEventSystem.SetSelectedGameObject(target);
            }
        }

        /// <summary>Returns the default selectable for a panel: Back for Help, Play otherwise.</summary>
        /// <param name="panel">The panel whose default selection is requested.</param>
        /// <returns>The button a joining controller focuses when no shared selection exists yet.</returns>
        private Selectable DefaultSelectable(EMenuPanel panel) =>
            panel == EMenuPanel.Help ? _helpBackButton : _playButton;

        /// <summary>Finds a named child of <paramref name="parent"/> and returns its <see cref="GameObject"/>.</summary>
        /// <param name="parent">The transform to search under.</param>
        /// <param name="name">The child name to find.</param>
        /// <param name="result">The found game object, or null if absent.</param>
        /// <returns>True when a matching child exists.</returns>
        private static bool TryFind(Transform parent, string name, out GameObject result)
        {
            result = parent.Find(name)?.gameObject;
            return result;
        }

        /// <summary>Finds a named child of <paramref name="parent"/> and returns its <typeparamref name="T"/> component.</summary>
        /// <typeparam name="T">The component type to retrieve.</typeparam>
        /// <param name="parent">The transform to search under.</param>
        /// <param name="name">The child name to find.</param>
        /// <param name="result">The found component, or null if the child or component is absent.</param>
        /// <returns>True when a matching child with the component exists.</returns>
        private static bool TryFind<T>(Transform parent, string name, out T result) where T : Component
        {
            result = parent.Find(name)?.GetComponent<T>();
            return result;
        }
    }
}
