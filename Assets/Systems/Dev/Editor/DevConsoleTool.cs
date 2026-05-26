using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using Systems.Combat;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.Combatant.Data;
using Systems.Common;
using Systems.Core;
using Systems.Core.ResourceManagement;
using Systems.Input;
using Systems.Stage;
using Systems.UI.Dev.InputHistory;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR


namespace Systems.Dev.Editor
{
    /// <summary>
    /// Editor-only loader that automatically opens <see cref="DevConsoleToolWindow"/> when the
    /// Editor enters Play Mode, saving the developer from opening it manually each run.
    /// </summary>
    [InitializeOnLoad]
    public class DevConsoleTool
    {
        /// <summary>Registers the play-mode listener that auto-opens the Dev Console.</summary>
        static DevConsoleTool()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>Opens <see cref="DevConsoleToolWindow"/> when Play Mode starts.</summary>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                DevConsoleToolWindow.ShowWindow();
            }
        }
    }

    /// <summary>
    /// Developer console EditorWindow for runtime inspection and control during Play Mode.
    /// Provides three tabs: Game Flow (trigger character select / combat with custom assets),
    /// Combat (swap input providers, toggle input history display), and Tick (time scale, manual tick).
    /// Requires Reflex injection which is performed automatically on Play Mode entry.
    /// Open via <c>Unwind → Runtime → Dev Console</c>.
    /// </summary>
    public class DevConsoleToolWindow : EditorWindow
    {
        /// <summary>Currently selected tab index: 0 = Game Flow, 1 = Combat.</summary>
        private int _selectedTab = 0;

        #region Game Flow Tab Content

        /// <summary>Whether the Combatant Select foldout is expanded.</summary>
        private bool _combatantSelectFoldout;

        /// <summary>Whether the Combat foldout is expanded.</summary>
        private bool _combatFoldout;

        /// <summary>Combatant 0 data asset used when starting a custom combat session from the tool.</summary>
        private CombatantDataSO _combatant0Data;

        /// <summary>Combatant 1 data asset used when starting a custom combat session from the tool.</summary>
        private CombatantDataSO _combatant1Data;

        /// <summary>Stage entry used when starting a custom combat session from the tool.</summary>
        private StageEntrySO _stageData;

        #endregion

        #region Combat Tab Content

        /// <summary>Popup index for the input provider assigned to combatant 0; 0 = None, 1+ = player slots.</summary>
        private int _combatant0InputIndex;

        /// <summary>Popup index for the input provider assigned to combatant 1; 0 = None, 1+ = player slots.</summary>
        private int _combatant1InputIndex;

        /// <summary>Mirrors the enabled state of the input history visualiser; toggled via a checkbox in the Combat tab.</summary>
        private bool _inputHistoryEnabled;

        #endregion

        #region Tick Tab Content

        /// <summary>Whether the tick manager runs automatically each frame.</summary>
        private bool _autoTick = true;

        /// <summary>Current time scale applied to the tick manager.</summary>
        private float _timeScale = 1.0f;

        #endregion

        /// <summary>Injected game manager; used to trigger game-state transitions.</summary>
        [Inject] private readonly GameManager _gameManager;

        /// <summary>Injected tick manager; used to read and set time scale and auto-tick.</summary>
        [Inject] private readonly TickManager _tickManager;

        /// <summary>Injected player registry; used to build the input-provider popup options.</summary>
        [Inject] private readonly PlayerRegistry _playerRegistry;

        /// <summary>Injected combat manager; used to read combatant state and reassign input providers.</summary>
        [Inject] private readonly CombatManager _combatManager;

        /// <summary>Injected input history list; shown or hidden via the Combat tab toggle.</summary>
        [Inject] private readonly InputHistoryUIList _inputHistoryUIList;

        /// <summary>True after Reflex attribute injection has completed; gates all runtime GUI rendering.</summary>
        private bool _injected;

        /// <summary>Opens or focuses the Dev Console window.</summary>
        [MenuItem("Unwind/Runtime/Dev Console")]
        public static void ShowWindow()
        {
            GetWindow<DevConsoleToolWindow>("Dev Console").Show();
        }

        /// <summary>
        /// Subscribes to play-mode change events and, if already in Play Mode, performs Reflex
        /// injection and wires game-state callbacks.
        /// </summary>
        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;


            if (!Application.isPlaying) return;

            // OnEnable fires both when the tool auto-opens via DevConsoleTool and when it is
            // opened manually after Play Mode has already started — both paths need injection.
            AttributeInjector.Inject(this, Container.RootContainer);
            _injected = true;

            _gameManager.OnGameStateChanged += OnGameStateChanged;
            _tickManager.SetTimeScale(_timeScale);
            _tickManager.SetAutoTick(_autoTick);
        }

        /// <summary>Unsubscribes event listeners and clears the injected flag on window close.</summary>
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            if (_injected)
            {
                _gameManager.OnGameStateChanged -= OnGameStateChanged;
                _injected = false;
            }
        }

        /// <summary>
        /// Resets transient state when exiting Play Mode and performs Reflex injection and
        /// event subscription when entering Play Mode.
        /// </summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _selectedTab = 0;
                _combatant0InputIndex = 0;
                _combatant1InputIndex = 0;
                _autoTick = true;
                _timeScale = 1.0f;
                Repaint();
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                AttributeInjector.Inject(this, Container.RootContainer);
                _injected = true;

                _gameManager.OnGameStateChanged += OnGameStateChanged;
                _combatManager.OnCombatStarted += OnCombatStarted;
                _tickManager.SetTimeScale(_timeScale);
                _tickManager.SetAutoTick(_autoTick);
                _inputHistoryEnabled = _inputHistoryUIList.enabled;
            }
        }

        /// <summary>Updates the input-provider popup indices by matching combatant input providers to registered player linkers.</summary>
        private void OnCombatStarted(CombatantBehaviour combatant0, CombatantBehaviour combatant1)
        {
            var list = _playerRegistry.GetAllPlayers();
            for (var index = 0; index < list.Count; index++)
            {
                var playerLinker = list[index];
                if (combatant0.InputProvider == playerLinker.PlayerInputProvider)
                {
                    _combatant0InputIndex = index + 1;
                }

                if (combatant1.InputProvider == playerLinker.PlayerInputProvider)
                {
                    _combatant1InputIndex = index + 1;
                }
            }
        }

        /// <summary>Requests a repaint when combat ends so the window reflects the new game state.</summary>
        private void OnCombatEnded()
        {
            Repaint();
        }

        /// <summary>Switches back to the Game Flow tab when combat ends, then repaints.</summary>
        private void OnGameStateChanged(GameState newState)
        {
            if (newState != GameState.Combat && _selectedTab == 1)
                _selectedTab = 0;

            Repaint();
        }

        /// <summary>Current height of the always-visible Tick panel at the bottom of the window.</summary>
        private float _tickTabHeight = 150f;

        /// <summary>Minimum height the Tick panel can be dragged to.</summary>
        private const float _minTickTabHeight = 60f;

        /// <summary>Height of the draggable splitter bar between the main tabs and the Tick panel.</summary>
        private const float _splitterHeight = 5f;

        /// <summary>True while the user is dragging the splitter handle.</summary>
        private bool _isDraggingSplitter = false;

        /// <summary>Renders the full window: tab bar, active tab content, splitter, and Tick panel.</summary>
        private void OnGUI()
        {
            if (!Application.isPlaying || !_injected)
            {
                DisplayPlayModeWarning();
                return;
            }

            DrawTabs();

            float totalHeight = position.height;
            float topAreaHeight = totalHeight - _tickTabHeight - _splitterHeight;

            Rect topRect = new Rect(0, EditorGUIUtility.singleLineHeight * 2, position.width, topAreaHeight);
            Rect splitterRect = new Rect(0, topRect.yMax, position.width, _splitterHeight);
            Rect bottomRect = new Rect(0, splitterRect.yMax, position.width, _tickTabHeight);

            // ── Top area (tabs) ────────────────────────────────────────────────────
            GUILayout.BeginArea(topRect);
            switch (_selectedTab)
            {
                case 0: DisplayGameFlowTab(); break;
                case 1: DisplayCombatTab(); break;
            }

            GUILayout.EndArea();

            // ── Splitter handle ────────────────────────────────────────────────────
            DrawSplitter(splitterRect);

            // ── Bottom area (tick panel) ───────────────────────────────────────────
            GUILayout.BeginArea(bottomRect);
            DisplayTickTab();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Draws a thin dark bar and handles mouse drag events to let the user resize the
        /// Tick panel by dragging the splitter up or down.
        /// </summary>
        private void DrawSplitter(Rect splitterRect)
        {
            // ── Visual ─────────────────────────────────────────────────────────────
            EditorGUI.DrawRect(splitterRect, new Color(0f, 0f, 0f, 0.3f));

            // ── Cursor feedback ────────────────────────────────────────────────────
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);

            // ── Drag logic ─────────────────────────────────────────────────────────
            Event e = Event.current;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(e.mousePosition))
                    {
                        _isDraggingSplitter = true;
                        e.Use();
                    }

                    break;

                case EventType.MouseDrag:
                    if (_isDraggingSplitter)
                    {
                        // delta.y is negative when dragging up (expanding bottom panel)
                        _tickTabHeight -= e.delta.y;
                        _tickTabHeight = Mathf.Max(_tickTabHeight, _minTickTabHeight);
                        _tickTabHeight = Mathf.Min(_tickTabHeight,
                            position.height - _minTickTabHeight - _splitterHeight);
                        Repaint();
                        e.Use();
                    }

                    break;

                case EventType.MouseUp:
                    if (_isDraggingSplitter)
                    {
                        _isDraggingSplitter = false;
                        e.Use();
                    }

                    break;
            }
        }

        /// <summary>Renders the Game Flow and Combat tab buttons; Combat is disabled outside combat state.</summary>
        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();

            DrawTabButton("Game Flow", 0);

            using (new EditorGUI.DisabledScope(_gameManager.CurrentGameState != GameState.Combat))
                DrawTabButton("Combat", 1);

            EditorGUILayout.EndHorizontal();
        }

        #region Game Flow Tab Methods

        /// <summary>Renders the Game Flow tab: combatant-select trigger and custom combat-start fields.</summary>
        private void DisplayGameFlowTab()
        {
            _combatantSelectFoldout =
                EditorGUILayout.BeginFoldoutHeaderGroup(_combatantSelectFoldout, "Combatant Select");

            if (_combatantSelectFoldout)
            {
                if (GUILayout.Button("Begin Combatant Select"))
                {
                    _gameManager.BeginCharacterSelect().Forget();
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            _combatFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_combatFoldout, "Combat");

            if (_combatFoldout)
            {
                _combatant0Data = (CombatantDataSO)EditorGUILayout.ObjectField("Combatant 0 Data", _combatant0Data,
                    typeof(CombatantDataSO), false);
                _combatant1Data = (CombatantDataSO)EditorGUILayout.ObjectField("Combatant 1 Data", _combatant1Data,
                    typeof(CombatantDataSO), false);
                _stageData =
                    (StageEntrySO)EditorGUILayout.ObjectField("Stage Data", _stageData, typeof(StageEntrySO), false);

                EditorGUILayout.Space();

                using (new EditorGUI.DisabledScope(!CanStartCombat()))
                {
                    if (GUILayout.Button("Begin Combat"))
                    {
                        BeginCombat().Forget();
                    }
                }
            }
        }

        /// <summary>True when all three assets required to start combat are assigned.</summary>
        private bool CanStartCombat()
        {
            return _combatant0Data && _combatant1Data && _stageData;
        }

        /// <summary>Constructs a <see cref="CombatEncounterData"/> from the inspector fields and starts combat via <see cref="GameManager"/>.</summary>
        private async UniTask BeginCombat()
        {
            var encounterData = new CombatEncounterData
            {
                Combatant0 = ToAssetReference(_combatant0Data),
                Combatant1 = ToAssetReference(_combatant1Data),
                Stage = ToAssetReference(_stageData)
            };
            
            await _gameManager.BeginCombat(encounterData, null, null);
        }

        #endregion


        #region Combat Tab Methods

        /// <summary>Renders the Combat tab: per-combatant input-provider selectors and the input-history toggle.</summary>
        private void DisplayCombatTab()
        {
            var players = _playerRegistry.GetAllPlayers();
            var options = BuildInputProviderOptions(players);

            GUILayout.BeginHorizontal();
            DisplayCombatantInformation(_combatManager.Combatant0Behaviour, CombatantSlot.Combatant0, options, players,
                ref _combatant0InputIndex);
            DisplayCombatantInformation(_combatManager.Combatant1Behaviour, CombatantSlot.Combatant1, options, players,
                ref _combatant1InputIndex);
            GUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();

            _inputHistoryEnabled = EditorGUILayout.Toggle("Enable Input History", _inputHistoryEnabled);

            if (EditorGUI.EndChangeCheck())
            {
                if (_inputHistoryEnabled)
                {
                    _inputHistoryUIList.Show();
                }
                else
                {
                    _inputHistoryUIList.Hide();
                }
            }
        }

        /// <summary>Renders one combatant column: name label, input-provider popup, current move, and state machine summary.</summary>
        private void DisplayCombatantInformation(CombatantBehaviour combatant, CombatantSlot slot, string[] options,
            List<PlayerLinker> players, ref int selectedIndex)
        {
            GUILayout.BeginVertical();
            GUILayout.Label($"{combatant.gameObject.name}");

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup("Input Provider", selectedIndex, options);

            if (EditorGUI.EndChangeCheck())
            {
                var provider = selectedIndex == 0 ? null : players[selectedIndex - 1].PlayerInputProvider;
                _combatManager.SetInputProvider(slot, provider);
            }

            GUILayout.Label($"Exe: {combatant.Runner.CurrentMove}");
            GUILayout.Label($"{combatant.StateMachine}");


            GUILayout.EndVertical();
        }

        /// <summary>Builds the popup option strings from the registered players, with "None" as index 0.</summary>
        private string[] BuildInputProviderOptions(List<PlayerLinker> players)
        {
            var options = new string[players.Count + 1];
            options[0] = "None";

            for (int i = 0; i < players.Count; i++)
                options[i + 1] = $"Player {i + 1} ({players[i].PlayerInput.name})";

            return options;
        }

        #endregion

        #region Tick Tab Methods

        /// <summary>Renders the always-visible Tick panel: auto-tick toggle, time-scale buttons, and a manual tick button.</summary>
        private void DisplayTickTab()
        {
            EditorGUI.BeginChangeCheck();

            _autoTick = EditorGUILayout.Toggle("Auto Tick", _autoTick);

            if (EditorGUI.EndChangeCheck())
            {
                _tickManager.SetAutoTick(_autoTick);
            }

            using (new EditorGUI.DisabledScope(!_autoTick))
            {
                EditorGUI.BeginChangeCheck();

                GUILayout.Label("Time Scale");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("0.5x"))
                {
                    _timeScale = 0.5f;
                }

                if (GUILayout.Button("1x"))
                {
                    _timeScale = 1f;
                }

                if (GUILayout.Button("2x"))
                {
                    _timeScale = 2f;
                }

                GUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    _tickManager.SetTimeScale(_timeScale);
                }
            }

            using (new EditorGUI.DisabledScope(_autoTick))
            {
                if (GUILayout.Button("Force Tick and Interpolate"))
                {
                    _tickManager.ForceTickAndInterpolate();
                }
            }
        }

        #endregion


        /// <summary>Renders an informational help box when the window is open outside Play Mode.</summary>
        private void DisplayPlayModeWarning()
        {
            EditorGUILayout.HelpBox("Enter Play Mode to access Developer Console features.", MessageType.Info);
            EditorGUILayout.Space();
        }

        /// <summary>Renders a toolbar-style toggle button that sets <see cref="_selectedTab"/> when pressed.</summary>
        private void DrawTabButton(string label, int index)
        {
            if (GUILayout.Toggle(_selectedTab == index, label, EditorStyles.toolbarButton))
                _selectedTab = index;
        }

        /// <summary>Converts a loaded asset to an <see cref="AssetReferenceT{T}"/> by looking up its GUID in the AssetDatabase.</summary>
        private static AssetReferenceT<T> ToAssetReference<T>(T asset) where T : Object
        {
            string path = AssetDatabase.GetAssetPath(asset);
            string guid = AssetDatabase.AssetPathToGUID(path);
            return new AssetReferenceT<T>(guid);
        }
    }
}
#endif