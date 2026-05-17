using System;
using Systems.Combat.Combatant.Data;
using Systems.UI.Common;
using TMPro;
using UnityEngine;

namespace Systems.UI.CombatantSelect
{
    [RequireComponent(typeof(UIMetadata))]
    public class CombatantSelectionButtonBinder : MonoBehaviour
    {
        [SerializeField] private TMP_Text combatantNameText;

        private void OnValidate()
        {
            if (!combatantNameText)
            {
                throw new Exception(
                    $"CombatantSelectionButtonBinder on {gameObject.name} is missing a reference to combatantNameText.");
            }

            var metadata = GetComponent<UIMetadata>();

            if (metadata.Value && metadata.Value is not CombatantSelectionDataSO)
            {
                throw new Exception(
                    $"CombatantSelectionButtonBinder on {gameObject.name} has a UIMetadata value that is not a CombatantSelectionDataSO.");
            }
        }

        private void Start()
        {
            var metadata = GetComponent<UIMetadata>();

            if (!metadata.Value)
            {
                combatantNameText.text = "Unassigned";
            }
            else if (metadata.Value is CombatantSelectionDataSO combatantSelectionDataSo)
            {
                combatantNameText.text = combatantSelectionDataSo.combatantDisplayName;
            }
        }
    }
}