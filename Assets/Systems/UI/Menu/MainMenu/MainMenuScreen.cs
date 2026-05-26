using System;
using System.Collections.Generic;
using Systems.Input;
using Systems.UI.Contracts;
using Systems.UI.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Systems.UI.Menu.MainMenu
{
    /// <summary>
    /// Drives the title screen. Any connected controller can navigate and confirm; they all share a
    /// single cursor, so identity does not matter here (player slots are decided later by join order in
    /// character select). Exposes the player's high-level intent via events and owns the in-canvas Help
    /// panel. All controller, cursor and action-map handling is delegated to <see cref="UIManager"/>.
    /// </summary>
    public class MainMenuScreen : IUIScreen, IDisposable
    {
        /// <summary>Raised when the player chooses to start the game.</summary>
        public event Action OnPlayRequested;

        /// <summary>Raised when the player chooses to quit the game.</summary>
        public event Action OnQuitRequested;

        /// <summary>Root canvas hosting the menu panels; toggled on by <see cref="Enter"/> and off by <see cref="Exit"/>.</summary>
        private readonly Canvas _canvas;

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

        /// <summary>The manager surface used to move the shared cursor and read attached controllers.</summary>
        private IUIContext _context;

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

        /// <summary>The panel currently shown; preserved across disconnects so a rejoin resumes here.</summary>
        private EMenuPanel _currentPanel = EMenuPanel.Main;

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
        /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
        /// <exception cref="InvalidOperationException">A required canvas element is missing or malformed.</exception>
        public MainMenuScreen(MainMenuCanvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

            if (!UIElementFinder.TryFind(_canvas.transform, MainPanelName, out _mainPanel))
                throw new InvalidOperationException($"MainMenuScreen: could not find '{MainPanelName}' in the canvas.");

            if (!UIElementFinder.TryFind(_canvas.transform, HelpPanelName, out _helpPanel))
                throw new InvalidOperationException($"MainMenuScreen: could not find '{HelpPanelName}' in the canvas.");

            UIElementFinder.TryFind(_mainPanel.transform, PlayButtonName, out _playButton);
            UIElementFinder.TryFind(_mainPanel.transform, HelpButtonName, out _helpButton);
            UIElementFinder.TryFind(_mainPanel.transform, QuitButtonName, out _quitButton);
            if (!_playButton || !_helpButton || !_quitButton)
                throw new InvalidOperationException(
                    $"MainMenuScreen: '{MainPanelName}' must contain Buttons named '{PlayButtonName}', '{HelpButtonName}' and '{QuitButtonName}'.");

            if (!UIElementFinder.TryFind(_helpPanel.transform, HelpBackButtonName, out _helpBackButton))
                throw new InvalidOperationException(
                    $"MainMenuScreen: '{HelpPanelName}' must contain a Button named '{HelpBackButtonName}'.");

            if (!UIElementFinder.TryFind(_helpPanel.transform, HelpScrollName, out ScrollRect helpScroll))
                throw new InvalidOperationException(
                    $"MainMenuScreen: '{HelpPanelName}' must contain a ScrollRect named '{HelpScrollName}'.");

            var helpText = helpScroll.content ? helpScroll.content.GetComponent<Text>() : null;
            if (!helpText)
                throw new InvalidOperationException($"MainMenuScreen: '{HelpScrollName}' content must have a Text component.");

            var asset = Resources.Load<TextAsset>(HelpContentPath);
            if (asset)
                helpText.text = asset.text;
            else
                Debug.LogWarning($"MainMenuScreen: Help content not found at Resources/{HelpContentPath}.txt.");

            _helpScroll = new HelpScrollController(helpScroll, ScrollUpActionName, ScrollDownActionName);
        }

        /// <inheritdoc/>
        public Contracts.CursorMode CursorMode => Contracts.CursorMode.Shared;

        /// <inheritdoc/>
        public Transform CursorParent => _canvas.transform;

        /// <summary>Stops Help scrolling on container teardown.</summary>
        public void Dispose() => _helpScroll.Stop();

        /// <inheritdoc/>
        public void Enter(IUIContext context)
        {
            _context = context;
            _canvas.gameObject.SetActive(true);
            ShowPanel(EMenuPanel.Main);
        }

        /// <inheritdoc/>
        public void Exit()
        {
            _helpScroll.Stop();
            _canvas.gameObject.SetActive(false);
            _context = null;
        }

        /// <inheritdoc/>
        public Selectable GetDefaultSelectable(int playerId) =>
            _currentPanel == EMenuPanel.Help ? _helpBackButton : _playButton;

        /// <inheritdoc/>
        public void OnPlayerAttached(PlayerLinker linker)
        {
            if (_currentPanel == EMenuPanel.Help) StartHelpScroll();
        }

        /// <inheritdoc/>
        public void OnPlayerDetached(PlayerLinker linker)
        {
            if (_currentPanel == EMenuPanel.Help) StartHelpScroll();
        }

        /// <inheritdoc/>
        public void OnNavigate(PlayerLinker linker, Selectable previous, Selectable current)
        {
            // Shared cursor mirroring is handled by the manager; nothing screen-specific to do.
        }

        /// <summary>
        /// Routes a submit to the matching intent or panel change; the shared cursor means every
        /// controller submits the same selectable.
        /// </summary>
        /// <param name="linker">The controller that submitted.</param>
        /// <param name="selectable">The selectable that was active when submit fired.</param>
        public void OnSubmit(PlayerLinker linker, Selectable selectable)
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
                    _context?.SetSharedSelection(_playButton);
                    break;

                case EMenuPanel.Help:
                    _mainPanel.SetActive(false);
                    _helpPanel.SetActive(true);
                    Canvas.ForceUpdateCanvases();
                    _helpScroll.ResetToTop();
                    _context?.SetSharedSelection(_helpBackButton);
                    StartHelpScroll();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(panel), panel, null);
            }
        }

        /// <summary>Starts Help scrolling, fed by the scroll actions of every controller on the menu.</summary>
        private void StartHelpScroll()
        {
            if (_context == null) return;

            var inputs = new List<PlayerInput>();
            foreach (var linker in _context.ActiveLinkers)
                if (linker && linker.PlayerInput)
                    inputs.Add(linker.PlayerInput);

            _helpScroll.Begin(inputs);
        }
    }
}
