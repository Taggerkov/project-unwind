using Systems.Combat.Combatant.Data;
using Systems.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.UI.Menu.CombatantSelect
{
    /// <summary>
    /// Populates a combatant selection button from its <see cref="UIMetadata"/> payload at runtime.
    /// Shows a "WiP" placeholder when no data is assigned so in-progress character slots remain
    /// visible and usable in play mode.
    /// </summary>
    [RequireComponent(typeof(UIMetadata))]
    public class CombatantSelectionButtonBinder : MonoBehaviour
    {
        /// <summary>TMP label written with the combatant's display name, or "WiP" when no data is set.</summary>
        [SerializeField] private TMP_Text combatantNameText;

        /// <summary>Image written with the combatant's thumbnail sprite when data is present.</summary>
        [SerializeField] private Image combatantThumbnailImage;

        /// <summary>
        /// Asserts that required serialised references are assigned and that the <see cref="UIMetadata"/>
        /// payload, when set, is a <see cref="CombatantSelectionDataSO"/>.
        /// </summary>
        private void OnValidate()
        {
            if (!combatantNameText)
                Debug.LogError($"CombatantSelectionButtonBinder on {gameObject.name} is missing a reference to combatantNameText.");

            if (!combatantThumbnailImage)
                Debug.LogError($"CombatantSelectionButtonBinder on {gameObject.name} is missing a reference to combatantThumbnailImage.");

            var metadata = GetComponent<UIMetadata>();
            if (metadata.Value && metadata.Value is not CombatantSelectionDataSO)
                Debug.LogError($"CombatantSelectionButtonBinder on {gameObject.name}: UIMetadata value is not CombatantSelectionDataSO.");
        }

        /// <summary>
        /// Reads the <see cref="UIMetadata"/> payload and applies the combatant display name and thumbnail
        /// to the UI elements. Falls back to "WiP" text when no data asset is assigned.
        /// </summary>
        private void Start()
        {
            var metadata = GetComponent<UIMetadata>();

            if (!metadata.Value)
            {
                combatantNameText.text = "WiP";
                return;
            }

            if (metadata.Value is not CombatantSelectionDataSO data) return;

            combatantNameText.text = data.combatantDisplayName;
            if (combatantThumbnailImage) combatantThumbnailImage.sprite = data.combatantThumbnail;
        }
    }
}