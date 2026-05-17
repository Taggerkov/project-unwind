using System;
using System.Collections.Generic;
using Systems.Combat.Combatant.Data;
using Systems.Common;
using Systems.Core;
using Systems.Input;
using Systems.UI.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Systems.UI.CombatantSelect
{
    public enum CharacterSelectState
    {
        CombatantSelection,
        StageSelection
    }

    public class CombatantSelectManager : IDisposable

    {
        /// <summary>
        /// Called when the encounter's configuration data is ready to be accessed.
        /// </summary>
        public event Action<CombatEncounterData> OnEncounterReady;


        private readonly PlayerRegistry _playerRegistry;
        private readonly Canvas _characterSelectCanvas;

        private readonly GameObject _combatantSelectionGrid;
        private readonly GameObject _stageSelectionLayout;

        private readonly Button _combatantSelectionButton1;
        private readonly Button _stageSelectionButton1;

        private Dictionary<int, PlayerUIHandler> _playerHandlers = new();

        private GameObject _sharedCursor;
        private readonly GameObject[] _individualCursors = new GameObject[2];

        private Action<InputAction.CallbackContext> _uiNavigateHandler;

        private CharacterSelectState _combatantSelectState;
        private CombatEncounterData _encounterData;


        #region Element IDs

        //Button id for querying

        private const string CombatantSelectionGridName = "CombatantSelectionGrid";

        #endregion

        public CombatantSelectManager(CharacterSelectCanvas canvas, PlayerRegistry playerRegistry)
        {
            if (playerRegistry == null)
            {
                Debug.LogError("CharacterSelectManager requires a PlayerRegistry instance.");
                return;
            }

            _characterSelectCanvas = canvas;

            var cursorGroup = _characterSelectCanvas.transform.Find("Cursors");

            _individualCursors[0] = cursorGroup.Find("P0Cursor")?.gameObject;
            _individualCursors[1] = cursorGroup.Find("P1Cursor")?.gameObject;

            if (!_individualCursors[0] || !_individualCursors[1])
            {
                Debug.LogError(
                    "CharacterSelectManager: Could not find P0Cursor and P1Cursor in the CharacterSelectCanvas.");
                return;
            }

            _sharedCursor = cursorGroup.Find("SharedCursor")?.gameObject;

            if (!_sharedCursor)
            {
                Debug.LogError(
                    "CharacterSelectManager: Could not find SharedCursor in the CharacterSelectCanvas.");
                return;
            }

            _combatantSelectionGrid = _characterSelectCanvas.transform.Find(CombatantSelectionGridName).gameObject;

            if (!_combatantSelectionGrid)
            {
                Debug.LogError(
                    $"CharacterSelectManager: Could not find '{CombatantSelectionGridName}' in the CharacterSelectCanvas.");
                return;
            }

            _combatantSelectionButton1 =
                _combatantSelectionGrid.transform.Find("CombatantSelectionButton")?.GetComponent<Button>();

            _stageSelectionLayout = _characterSelectCanvas.transform.Find("StageSelectionLayout")?.gameObject;

            if (!_stageSelectionLayout)
            {
                Debug.LogError(
                    "CharacterSelectManager: Could not find 'StageSelectionLayout' in the CharacterSelectCanvas.");
                return;
            }

            _stageSelectionButton1 =
                _stageSelectionLayout.transform.Find("StageSelectionButton")?.GetComponent<Button>();

            if (!_stageSelectionButton1)
            {
                Debug.LogError(
                    "CharacterSelectManager: Could not find 'StageSelectionButton' in the StageSelectionLayout.");
                return;
            }

            _playerRegistry = playerRegistry;
        }

        public void Dispose()
        {
        }

        public void Begin()
        {
            _characterSelectCanvas.gameObject.SetActive(true);
            BeginCharacterSelection();

            _playerRegistry.OnPlayerJoined += HandlePlayerJoined;
        }

        private void BeginCharacterSelection()
        {
            _combatantSelectState = CharacterSelectState.CombatantSelection;

            _combatantSelectionGrid.SetActive(true);
            _stageSelectionLayout.SetActive(false);

            // Force the canvas to update so that the layout is calculated, if not, sizes and positions will be wrong until the end of frame.
            Canvas.ForceUpdateCanvases();

            var players = _playerRegistry.GetAllPlayers();

            foreach (var playerLinker in players)
            {
                HandlePlayerJoined(playerLinker);
            }
        }

        private void BeginStageSelection()
        {
            _combatantSelectState = CharacterSelectState.StageSelection;

            _combatantSelectionGrid.SetActive(false);
            _stageSelectionLayout.SetActive(true);

            Canvas.ForceUpdateCanvases();

            var p0Handler = _playerHandlers[0];

            p0Handler.UIState.CurrentSelectable = _stageSelectionButton1;
            p0Handler.UIState.CurrentSelectableRectTransform = _stageSelectionButton1.GetComponent<RectTransform>();

            p0Handler.EnableUI();

            RefreshCursors();
        }

        private void EndStageSelection()
        {
            _playerRegistry.OnPlayerJoined -= HandlePlayerJoined;
            
            _playerHandlers[0].DisableUI();

            _characterSelectCanvas.gameObject.SetActive(false);
            _combatantSelectionGrid.SetActive(false);
            _stageSelectionLayout.SetActive(false);

            SendEncounterData();
        }

        private void HandlePlayerJoined(PlayerLinker playerLinker)
        {
            playerLinker.OnUINavigate += OnPlayerNavigate;
            playerLinker.OnUISubmit += OnPlayerSubmit;

            var playerCursor = _individualCursors[playerLinker.PlayerId];

            var targetSelectable = _combatantSelectionButton1;

            var newState = new PlayerUIState
            {
                Cursor = playerCursor,
                CursorRectTransform = playerCursor.GetComponent<RectTransform>(),
                CurrentSelectable = targetSelectable,
                CurrentSelectableRectTransform = targetSelectable.GetComponent<RectTransform>()
            };

            var newHandler = new PlayerUIHandler
            {
                PlayerLinker = playerLinker,
                UIState = newState
            };

            newHandler.EnableUI();

            _playerHandlers[playerLinker.PlayerId] = newHandler;

            // Initial visual refresh
            RefreshCursors();
        }

        private void OnPlayerSubmit(PlayerLinker linker, Selectable selectable)
        {
            Debug.Log($"Player{linker.PlayerId} submitted on '{selectable.name}'.");

            if (selectable.TryGetComponent<CombatantSelectionButtonBinder>(out _))
            {
                HandleCombatantSelection(linker, selectable);
            }

            if (selectable.TryGetComponent<StageSelectionButtonBinder>(out _))
            {
                HandleStageSelection(linker, selectable);
            }
        }

        private void OnPlayerNavigate(PlayerLinker linker, Selectable from, Selectable to)
        {
            if (!_playerHandlers.TryGetValue(linker.PlayerId, out var handler)) return;

            var state = handler.UIState;

            // 1. Update the state
            state.CurrentSelectable = to;
            state.CurrentSelectableRectTransform = to.GetComponent<RectTransform>();

            // 2. Decide what cursors to show
            RefreshCursors();
        }

        private void RefreshCursors()
        {
            bool overlapping = IsOverlapDetected(out Selectable overlapPoint);

            if (overlapping)
            {
                HideAllIndividualCursors();
                _sharedCursor.SetActive(true);
                _sharedCursor.transform.position = overlapPoint.transform.position;

                var selectableRect = overlapPoint.GetComponent<RectTransform>();
                var sharedCursorRect = _sharedCursor.GetComponent<RectTransform>();
                if (selectableRect && sharedCursorRect)
                {
                    sharedCursorRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                        selectableRect.rect.width + 15);
                    sharedCursorRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                        selectableRect.rect.height + 15);
                }
            }
            else
            {
                _sharedCursor.SetActive(false);
                foreach (var handler in _playerHandlers.Values)
                {
                    var state = handler.UIState;

                    if (!state.Enabled)
                    {
                        continue;
                    }


                    if (state.CurrentSelectable)
                    {
                        state.Cursor.SetActive(true);
                        state.Cursor.transform.position = state.CurrentSelectable.transform.position;

                        if (state.CurrentSelectableRectTransform && state.CursorRectTransform)
                        {
                            state.CursorRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                                state.CurrentSelectableRectTransform.rect.width + 15);
                            state.CursorRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                                state.CurrentSelectableRectTransform.rect.height + 15);
                        }
                    }
                }
            }
        }

        private bool IsOverlapDetected(out Selectable overlapPoint)
        {
            overlapPoint = null;

            if (_playerHandlers.Count < 2) return false;

            if (!_playerHandlers[0].UIState.Enabled || !_playerHandlers[1].UIState.Enabled) return false;

            var p0 = _playerHandlers[0].UIState.CurrentSelectable;
            var p1 = _playerHandlers[1].UIState.CurrentSelectable;

            if (p0 && p0 == p1)
            {
                overlapPoint = p0;
                return true;
            }

            return false;
        }

        private void HideAllIndividualCursors()
        {
            foreach (var handler in _playerHandlers.Values)
            {
                handler.UIState.Cursor.SetActive(false);
            }
        }

        private void HandleCombatantSelection(PlayerLinker linker, Selectable selectable)
        {
            var metadata = selectable.GetComponent<UIMetadata>();

            if (!metadata.Value || metadata.Value is not CombatantSelectionDataSO selectionDataSo) return;

            int slotToFill = -1;

            if (_playerHandlers.Count == 1)
            {
                if (_encounterData.Combatant0 == null)
                {
                    _encounterData.Combatant0 = selectionDataSo.combatantDataReference;
                    slotToFill = 0;
                }
                else if (_encounterData.Combatant1 == null)
                {
                    _encounterData.Combatant1 = selectionDataSo.combatantDataReference;
                    slotToFill = 1;
                    _playerHandlers[0].DisableUI();
                    RefreshCursors();
                }
            }
            else
            {
                switch (linker.PlayerId)
                {
                    case 0:
                        _encounterData.Combatant0 = selectionDataSo.combatantDataReference;
                        slotToFill = 0;
                        _playerHandlers[0].DisableUI();
                        RefreshCursors();
                        break;
                    case 1:
                        _encounterData.Combatant1 = selectionDataSo.combatantDataReference;
                        slotToFill = 1;
                        _playerHandlers[1].DisableUI();
                        RefreshCursors();
                        break;
                    default:
                        Debug.LogError($"Player{linker.PlayerId} has an invalid ID!");
                        break;
                }
            }

            Debug.Log(
                $"Player{linker.PlayerId} selected combatant '{selectionDataSo.combatantDisplayName}' on slot {slotToFill}.");


            CheckIfCombatantSelectionComplete();
        }

        private void HandleStageSelection(PlayerLinker linker, Selectable selectable)
        {
            var metadata = selectable.GetComponent<UIMetadata>();

            if (!metadata.Value || metadata.Value is not StageSelectionDataSO selectionDataSo) return;

            _encounterData.Stage = selectionDataSo.stageEntryReference;

            EndStageSelection();
        }

        private void CheckIfCombatantSelectionComplete()
        {
            if (_encounterData is { Combatant0: not null, Combatant1: not null })
            {
                BeginStageSelection();
            }
        }

        private void SendEncounterData()
        {
            OnEncounterReady?.Invoke(_encounterData);
        }
    }
}