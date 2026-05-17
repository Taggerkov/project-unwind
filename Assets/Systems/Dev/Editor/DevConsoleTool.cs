using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using Systems.Combat;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.Combatant.Data;
using Systems.Core;
using Systems.Input;
using Systems.Stage;
using Systems.UI.CombatantSelect;
using Systems.UI.Dev.InputHistory.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR


namespace Systems.Dev.Editor
{
    [InitializeOnLoad]
    public class DevConsoleTool
    {
        static DevConsoleTool()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // Open the Dev Console window when entering play mode
                DevConsoleToolWindow.ShowWindow();
            }
        }
    }

    public class DevConsoleToolWindow : EditorWindow
    {
        private int _selectedTab = 0;

        #region Game Flow Tab Content

        private bool _combatantSelectFoldout;
        private bool _combatFoldout;

        private CombatantDataSO _combatant0Data;
        private CombatantDataSO _combatant1Data;
        private StageEntrySO _stageData;

        #endregion

        #region Combat Tab Content

        private int _combatant0InputIndex;
        private int _combatant1InputIndex;

        private bool _inputHistoryEnabled;

        #endregion

        #region Tick Tab Content

        private bool _autoTick = true;
        private float _timeScale = 1.0f;

        #endregion

        [Inject] private readonly GameManager _gameManager;
        [Inject] private readonly CombatantSelectManager _combatantSelectManager;
        [Inject] private readonly TickManager _tickManager;
        [Inject] private readonly PlayerRegistry _playerRegistry;
        [Inject] private readonly CombatManager _combatManager;
        [Inject] private readonly InputHistoryUIList _inputHistoryUIList;

        private bool _injected;

        [MenuItem("Tools/Dev Console")]
        public static void ShowWindow()
        {
            GetWindow<DevConsoleToolWindow>("Dev Console").Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;


            if (!Application.isPlaying) return;

            //If the tool opened automatically on play mode enter / was opened manually after.

            AttributeInjector.Inject(this, Container.RootContainer);
            _injected = true;

            _gameManager.OnGameStateChanged += OnGameStateChanged;
            _tickManager.SetTimeScale(_timeScale);
            _tickManager.SetAutoTick(_autoTick);
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            if (_injected)
            {
                _gameManager.OnGameStateChanged -= OnGameStateChanged;
                _injected = false;
            }
        }

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

        private void OnCombatEnded()
        {
            Repaint();
        }

        private void OnGameStateChanged(GameState newState)
        {
            if (newState != GameState.Combat && _selectedTab == 1)
                _selectedTab = 0;

            Repaint();
        }

        private float _tickTabHeight = 150f;
        private const float _minTickTabHeight = 60f;
        private const float _splitterHeight = 5f;
        private bool _isDraggingSplitter = false;

        private void OnGUI()
        {
            if (!Application.isPlaying || !_injected)
            {
                DisplayPlayModeWarning();
                return;
            }

            DrawTabs();

            // Calculate rects
            float totalHeight = position.height;
            float topAreaHeight = totalHeight - _tickTabHeight - _splitterHeight;

            Rect topRect = new Rect(0, EditorGUIUtility.singleLineHeight * 2, position.width, topAreaHeight);
            Rect splitterRect = new Rect(0, topRect.yMax, position.width, _splitterHeight);
            Rect bottomRect = new Rect(0, splitterRect.yMax, position.width, _tickTabHeight);

            // --- Top area (tabs) ---
            GUILayout.BeginArea(topRect);
            switch (_selectedTab)
            {
                case 0: DisplayGameFlowTab(); break;
                case 1: DisplayCombatTab(); break;
            }

            GUILayout.EndArea();

            // --- Splitter handle ---
            DrawSplitter(splitterRect);

            // --- Bottom area (tick tab) ---
            GUILayout.BeginArea(bottomRect);
            DisplayTickTab();
            GUILayout.EndArea();
        }

        private void DrawSplitter(Rect splitterRect)
        {
            // Visual
            EditorGUI.DrawRect(splitterRect, new Color(0f, 0f, 0f, 0.3f));

            // Cursor feedback
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);

            // Drag logic
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

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();

            DrawTabButton("Game Flow", 0);

            using (new EditorGUI.DisabledScope(_gameManager.CurrentGameState != GameState.Combat))
                DrawTabButton("Combat", 1);

            EditorGUILayout.EndHorizontal();
        }

        #region Game Flow Tab Methods

        private void DisplayGameFlowTab()
        {
            _combatantSelectFoldout =
                EditorGUILayout.BeginFoldoutHeaderGroup(_combatantSelectFoldout, "Combatant Select");

            if (_combatantSelectFoldout)
            {
                if (GUILayout.Button("Begin Combatant Select"))
                {
                    _gameManager.BeginCharacterSelect();
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

        private bool CanStartCombat()
        {
            return _combatant0Data && _combatant1Data && _stageData;
        }

        private async UniTask BeginCombat()
        {
            var sceneInstance =
                await Addressables.LoadSceneAsync(_stageData.sceneReference.Path, LoadSceneMode.Additive, false);
            _gameManager.BeginCombat(sceneInstance, _combatant0Data, _combatant1Data, null, null).Forget();
        }

        #endregion


        #region Combat Tab Methods

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

            //Add a checkbox

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

                //Buttons to set the time scale to 0.5, 1, and 2 for testing slow motion and fast forward

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


        private void DisplayPlayModeWarning()
        {
            EditorGUILayout.HelpBox("Enter Play Mode to access Developer Console features.", MessageType.Info);
            EditorGUILayout.Space();
        }


        private void DrawTabButton(string label, int index)
        {
            if (GUILayout.Toggle(_selectedTab == index, label, EditorStyles.toolbarButton))
                _selectedTab = index;
        }
    }
}
#endif