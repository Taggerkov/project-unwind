using System;
using Reflex.Attributes;
using Systems.Combat.Combatant.Data;
using Systems.Common;
using Systems.Core;
using Systems.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems
{
    public class CharacterSelectManager : IDisposable

    {
        /// <summary>
        /// Called when the encounter's configuration data is ready to be accessed.
        /// </summary>
        public event Action<CombatEncounterData> OnEncounterReady;

        private CombatEncounterData _encounterData;

        private readonly UIDocument _document;
        
        private readonly PlayerRegistry _playerRegistry;


        #region Element IDs

        //Button id for querying

        private const string CombatantSelectionButtonId = "btn-combatant-select";

        #endregion

        public CharacterSelectManager(UIDocument document, PlayerRegistry playerRegistry)
        {
            if (!document)
            {
                Debug.LogError("CharacterSelectManager requires a UIDocument component.");
                return;
            }

            _document = document;
            
            
            
            if (playerRegistry == null)
            {
                Debug.LogError("CharacterSelectManager requires a PlayerRegistry instance.");
                return;
            }
            
            _playerRegistry = playerRegistry;
            
            BindDocument(_document);
            _playerRegistry.OnPlayerJoined += HandlePlayerJoined;
        }

        public void Dispose()
        {
            if (_document)
            {
                UnbindDocument(_document);
            }

            _playerRegistry.OnPlayerJoined -= HandlePlayerJoined;
        }

        public void BeginCharacterSelection()
        {
            FocusFirstAvailableButton();
        }

        private void HandlePlayerJoined(PlayerLinker playerLinker)
        {
            Debug.Log($"CharacterSelectManager: Player '{playerLinker.name}' joined. Focusing first available button.");
            // FocusFirstAvailableButton();
        }

        private void FocusFirstAvailableButton()
        {
            var root = _document.rootVisualElement;
            var combatantSelectionButton = root.Q<Button>(className: CombatantSelectionButtonId);

            combatantSelectionButton?.Focus();
        }


        private void BindDocument(UIDocument document)
        {
            var root = document.rootVisualElement;

            var combatantSelectionButtons = root.Query<Button>(className: CombatantSelectionButtonId).ToList();

            foreach (var button in combatantSelectionButtons)
            {
                if (button.dataSource is not CombatantSelectionDataSO)
                {
                    Debug.LogWarning(
                        $"Button '{button.name}' does not have a valid CombatantSelectionDataSO as its data source. Skipping.");
                    continue;
                }

                button.RegisterCallback<ClickEvent>(OnCombatantButtonClicked);
            }
        }

        private void UnbindDocument(UIDocument document)
        {
            var root = document.rootVisualElement;

            var combatantSelectionButtons = root.Query<Button>(className: CombatantSelectionButtonId).ToList();

            foreach (var button in combatantSelectionButtons)
            {
                button.UnregisterCallback<ClickEvent>(OnCombatantButtonClicked);
            }
        }

        private void OnCombatantButtonClicked(ClickEvent evt)
        {
            if (evt.currentTarget is not Button clickedButton) return;
            var data = clickedButton.dataSource as CombatantSelectionDataSO;

            if (data)
            {
                RegisterCombatantSelection(data);
            }
        }

        private void RegisterCombatantSelection(CombatantSelectionDataSO combatantData)
        {
            Debug.Log($"Character Selected: {combatantData.combatantDisplayName}");

            _encounterData.Combatant0 = combatantData.combatantDataReference;


            SendEncounterData();
        }

        private void SendEncounterData()
        {
            OnEncounterReady?.Invoke(_encounterData);
        }
    }
}