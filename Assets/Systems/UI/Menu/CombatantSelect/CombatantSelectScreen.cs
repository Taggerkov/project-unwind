using System;
using Systems.Combat.Combatant.Data;
using Systems.Common;
using Systems.Input;
using Systems.UI.Contracts;
using Systems.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.UI.Menu.CombatantSelect
{
    /// <summary>The two stages of the character select flow.</summary>
    public enum CombatantSelectState
    {
        /// <summary>Players are picking their combatants.</summary>
        CombatantSelection,

        /// <summary>Player 0 is picking the stage.</summary>
        StageSelection
    }

    /// <summary>
    /// Drives the character select screen. Unlike the main menu, identity matters: each player has its
    /// own cursor and fills its own combatant slot, then player 0 picks the stage. Controller, cursor
    /// and action-map handling are delegated to <see cref="UIManager"/>; this screen owns only the
    /// selection state machine and the resulting <see cref="CombatEncounterData"/>.
    /// </summary>
    public class CombatantSelectScreen : IUIScreen
    {
        /// <summary>Raised when the encounter's configuration is complete and ready to load.</summary>
        public event Action<CombatEncounterData> OnEncounterReady;

        /// <summary>Root canvas toggled on by <see cref="Enter"/> and off by <see cref="Exit"/>.</summary>
        private readonly Canvas _characterSelectCanvas;

        /// <summary>Panel hosting the grid of combatant buttons; shown during <see cref="CombatantSelectState.CombatantSelection"/>.</summary>
        private readonly GameObject _combatantSelectionGrid;

        /// <summary>Panel hosting the stage buttons; shown during <see cref="CombatantSelectState.StageSelection"/>.</summary>
        private readonly GameObject _stageSelectionLayout;

        /// <summary>First (and currently only) combatant button; used as the default focus during combatant selection.</summary>
        private readonly Button _combatantSelectionButton1;

        /// <summary>First (and currently only) stage button; used as the default focus during stage selection.</summary>
        private readonly Button _stageSelectionButton1;

        /// <summary>Optional title label updated by <see cref="UpdateTitle"/>; absent when the canvas omits the element.</summary>
        private readonly Text _title;

        /// <summary>Optional status label for player 0 updated by <see cref="UpdateStatusLabels"/>.</summary>
        private readonly Text _p0StatusLabel;

        /// <summary>Optional status label for player 1 updated by <see cref="UpdateStatusLabels"/>.</summary>
        private readonly Text _p1StatusLabel;

        /// <summary>The manager surface retained for the screen's lifetime; null between <see cref="Exit"/> and the next <see cref="Enter"/>.</summary>
        private IUIContext _context;

        /// <summary>Which phase of the flow is currently active.</summary>
        private CombatantSelectState _state;

        /// <summary>Accumulates the combatant and stage choices built up during this flow.</summary>
        private CombatEncounterData _encounterData;

        /// <summary>Display name of the character picked for slot 0; null until a selection is confirmed.</summary>
        private string _p0DisplayName;

        /// <summary>Display name of the character picked for slot 1; null until a selection is confirmed.</summary>
        private string _p1DisplayName;

        /// <summary>The player currently driving stage selection, or -1 when none.</summary>
        private int _stagePlayerId = -1;

        #region Element names

        private const string CombatantSelectionGridName = "CombatantSelectionGrid";
        private const string CombatantSelectionButtonName = "CombatantSelectionButton";
        private const string StageSelectionLayoutName = "StageSelectionLayout";
        private const string StageSelectionButtonName = "StageSelectionButton";
        private const string TitleName = "Title";
        private const string P0StatusLabelName = "P0StatusLabel";
        private const string P1StatusLabelName = "P1StatusLabel";

        #endregion

        /// <summary>
        /// Resolves every required canvas element by name. Throws if any dependency or element is
        /// missing: character select is mandatory, so a malformed canvas must fail loudly at
        /// construction rather than yield a half-built screen.
        /// </summary>
        /// <param name="canvas">Strongly typed wrapper around the character select root canvas.</param>
        /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
        /// <exception cref="InvalidOperationException">A required canvas element is missing.</exception>
        public CombatantSelectScreen(CombatantSelectCanvas canvas)
        {
            _characterSelectCanvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

            if (!UIElementFinder.TryFind(_characterSelectCanvas.transform, CombatantSelectionGridName, out _combatantSelectionGrid))
                throw new InvalidOperationException($"CombatantSelectScreen: could not find '{CombatantSelectionGridName}' in the canvas.");

            if (!UIElementFinder.TryFind(_combatantSelectionGrid.transform, CombatantSelectionButtonName, out _combatantSelectionButton1))
                throw new InvalidOperationException(
                    $"CombatantSelectScreen: '{CombatantSelectionGridName}' must contain a Button named '{CombatantSelectionButtonName}'.");

            if (!UIElementFinder.TryFind(_characterSelectCanvas.transform, StageSelectionLayoutName, out _stageSelectionLayout))
                throw new InvalidOperationException($"CombatantSelectScreen: could not find '{StageSelectionLayoutName}' in the canvas.");

            if (!UIElementFinder.TryFind(_stageSelectionLayout.transform, StageSelectionButtonName, out _stageSelectionButton1))
                throw new InvalidOperationException(
                    $"CombatantSelectScreen: '{StageSelectionLayoutName}' must contain a Button named '{StageSelectionButtonName}'.");

            UIElementFinder.TryFind(_characterSelectCanvas.transform, TitleName, out _title);
            UIElementFinder.TryFind(_characterSelectCanvas.transform, P0StatusLabelName, out _p0StatusLabel);
            UIElementFinder.TryFind(_characterSelectCanvas.transform, P1StatusLabelName, out _p1StatusLabel);
        }

        /// <inheritdoc/>
        public Contracts.CursorMode CursorMode => Contracts.CursorMode.PerPlayer;

        /// <inheritdoc/>
        public Transform CursorParent => _characterSelectCanvas.transform;

        /// <inheritdoc/>
        public void Enter(IUIContext context)
        {
            _context = context;
            _characterSelectCanvas.gameObject.SetActive(true);
            BeginCombatantSelection();
        }

        /// <inheritdoc/>
        public void Exit()
        {
            _characterSelectCanvas.gameObject.SetActive(false);
            _context = null;
        }

        /// <inheritdoc/>
        public Selectable GetDefaultSelectable(int playerId) =>
            _state == CombatantSelectState.StageSelection ? _stageSelectionButton1 : _combatantSelectionButton1;

        /// <inheritdoc/>
        public void OnPlayerAttached(PlayerLinker linker)
        {
            // The manager places the cursor; the encounter state machine needs nothing extra on attach.
        }

        /// <inheritdoc/>
        public void OnPlayerDetached(PlayerLinker linker)
        {
            // Slot choices live in the encounter data and the cursor holds the player's selection,
            // so a disconnect needs no cleanup here; the player resumes on reconnect.
        }

        /// <inheritdoc/>
        public void OnNavigate(PlayerLinker linker, Selectable previous, Selectable current)
        {
            // Per-player cursor placement is handled by the manager.
        }

        /// <inheritdoc/>
        public void OnSubmit(PlayerLinker linker, Selectable selectable)
        {
            if (selectable.TryGetComponent<CombatantSelectionButtonBinder>(out _))
                HandleCombatantSelection(linker, selectable);
            else if (selectable.TryGetComponent<StageSelectionButtonBinder>(out _))
                HandleStageSelection(selectable);
        }

        /// <summary>Resets the encounter, shows the combatant grid, and hides the stage layout.</summary>
        private void BeginCombatantSelection()
        {
            _state = CombatantSelectState.CombatantSelection;
            _encounterData = new CombatEncounterData();
            _stagePlayerId = -1;
            _p0DisplayName = null;
            _p1DisplayName = null;

            _combatantSelectionGrid.SetActive(true);
            _stageSelectionLayout.SetActive(false);
            Canvas.ForceUpdateCanvases();
            UpdateTitle();
            UpdateStatusLabels();
        }

        /// <summary>Shows the stage layout and hands control to a single player, guarding for an absent player 0.</summary>
        private void BeginStageSelection()
        {
            _state = CombatantSelectState.StageSelection;

            _combatantSelectionGrid.SetActive(false);
            _stageSelectionLayout.SetActive(true);
            Canvas.ForceUpdateCanvases();
            UpdateTitle();

            _stagePlayerId = LowestActivePlayerId();
            if (_stagePlayerId < 0) return;

            SetPlayerEnabled(_stagePlayerId, true);
            _context?.SetSelection(_stagePlayerId, _stageSelectionButton1);
        }

        /// <summary>Disables the stage player's input, hides the screen, and publishes the encounter data.</summary>
        private void EndStageSelection()
        {
            if (_stagePlayerId >= 0) SetPlayerEnabled(_stagePlayerId, false);
            OnEncounterReady?.Invoke(_encounterData);
        }

        /// <summary>
        /// Records a combatant pick into the next free slot and locks the picking player out of further
        /// input. With one player connected, that player fills both slots in turn.
        /// </summary>
        /// <param name="linker">The controller that picked.</param>
        /// <param name="selectable">The combatant button that was submitted.</param>
        private void HandleCombatantSelection(PlayerLinker linker, Selectable selectable)
        {
            if (!selectable.TryGetComponent<UIMetadata>(out var metadata))
            {
                Debug.LogError($"CombatantSelectScreen: submitted selectable '{selectable.name}' has no UIMetadata.");
                return;
            }
            if (metadata.Value is not CombatantSelectionDataSO selectionData)
            {
                Debug.LogError($"CombatantSelectScreen: UIMetadata on '{selectable.name}' is not CombatantSelectionDataSO.");
                return;
            }

            if (_context.ActiveLinkers.Count <= 1)
            {
                if (_encounterData.Combatant0 == null)
                {
                    _encounterData.Combatant0 = selectionData.combatantDataReference;
                    _p0DisplayName = selectionData.combatantDisplayName;
                }
                else if (_encounterData.Combatant1 == null)
                {
                    _encounterData.Combatant1 = selectionData.combatantDataReference;
                    _p1DisplayName = selectionData.combatantDisplayName;
                    SetPlayerEnabled(linker.PlayerId, false);
                }
            }
            else
            {
                switch (linker.PlayerId)
                {
                    case 0:
                        _encounterData.Combatant0 = selectionData.combatantDataReference;
                        _p0DisplayName = selectionData.combatantDisplayName;
                        SetPlayerEnabled(0, false);
                        break;
                    case 1:
                        _encounterData.Combatant1 = selectionData.combatantDataReference;
                        _p1DisplayName = selectionData.combatantDisplayName;
                        SetPlayerEnabled(1, false);
                        break;
                    default:
                        Debug.LogError($"CombatantSelectScreen: player {linker.PlayerId} has an invalid id.");
                        return;
                }
            }

            UpdateStatusLabels();

            if (_encounterData is { Combatant0: not null, Combatant1: not null })
                BeginStageSelection();
        }

        /// <summary>Records the chosen stage and completes the flow.</summary>
        /// <param name="selectable">The stage button that was submitted.</param>
        private void HandleStageSelection(Selectable selectable)
        {
            if (!selectable.TryGetComponent<UIMetadata>(out var metadata))
            {
                Debug.LogError($"CombatantSelectScreen: submitted selectable '{selectable.name}' has no UIMetadata.");
                return;
            }
            if (metadata.Value is not StageSelectionDataSO selectionData)
            {
                Debug.LogError($"CombatantSelectScreen: UIMetadata on '{selectable.name}' is not StageSelectionDataSO.");
                return;
            }

            _encounterData.Stage = selectionData.stageEntryReference;
            EndStageSelection();
        }

        /// <summary>Updates <see cref="_title"/> text to reflect the current <see cref="_state"/>.</summary>
        private void UpdateTitle()
        {
            if (_title) _title.text = _state == CombatantSelectState.CombatantSelection
                ? "Select Your Fighter"
                : "Select Stage";
        }

        /// <summary>Refreshes <see cref="_p0StatusLabel"/> and <see cref="_p1StatusLabel"/> with the confirmed display names or a "Selecting…" placeholder.</summary>
        private void UpdateStatusLabels()
        {
            if (_p0StatusLabel)
                _p0StatusLabel.text = string.IsNullOrEmpty(_p0DisplayName) ? "P1 — Selecting..." : $"P1 — {_p0DisplayName}";
            if (_p1StatusLabel)
                _p1StatusLabel.text = string.IsNullOrEmpty(_p1DisplayName) ? "P2 — Selecting..." : $"P2 — {_p1DisplayName}";
        }

        /// <summary>Null-safe helper that delegates to <see cref="IUIContext.SetPlayerEnabled"/>; no-op when <see cref="_context"/> is null.</summary>
        private void SetPlayerEnabled(int playerId, bool enabled) =>
            _context?.SetPlayerEnabled(playerId, enabled);

        /// <summary>Returns the lowest attached player id (player 0 when present), or -1 if none.</summary>
        private int LowestActivePlayerId()
        {
            var linkers = _context.ActiveLinkers;
            return linkers.Count > 0 ? linkers[0].PlayerId : -1;
        }
    }
}
