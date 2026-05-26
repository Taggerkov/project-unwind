using Systems.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.UI.Menu.CombatantSelect
{
    /// <summary>
    /// Populates a stage selection button from its <see cref="UIMetadata"/> payload at runtime.
    /// Blacks out the thumbnail when no data is assigned so in-progress stage slots degrade gracefully
    /// in play mode. An optional display name override allows WiP labels without a separate data asset.
    /// </summary>
    [RequireComponent(typeof(UIMetadata))]
    public class StageSelectionButtonBinder : MonoBehaviour
    {
        /// <summary>Image written with the stage thumbnail sprite when data is present; blacked out otherwise.</summary>
        [SerializeField] private Image stageImage;

        /// <summary>TMP label written with the stage display name or <see cref="displayNameOverride"/> when data is present.</summary>
        [SerializeField] private TMP_Text stageNameText;

        /// <summary>
        /// When non-empty, shown in the label instead of the data asset's own display name. Used for
        /// WiP placeholder slots that share a real stage's thumbnail image.
        /// </summary>
        [SerializeField] private string displayNameOverride;

        /// <summary>
        /// Asserts that required serialised references are assigned and that the <see cref="UIMetadata"/>
        /// payload, when set, is a <see cref="StageSelectionDataSO"/>.
        /// </summary>
        private void OnValidate()
        {
            if (!stageImage)
                Debug.LogError($"StageSelectionButtonBinder on {gameObject.name} is missing a reference to stageImage.");

            if (!stageNameText)
                Debug.LogError($"StageSelectionButtonBinder on {gameObject.name} is missing a reference to stageNameText.");

            var metadata = GetComponent<UIMetadata>();
            if (metadata.Value && metadata.Value is not StageSelectionDataSO)
                Debug.LogError($"StageSelectionButtonBinder on {gameObject.name}: UIMetadata value is not StageSelectionDataSO.");
        }

        /// <summary>
        /// Reads the <see cref="UIMetadata"/> payload and applies the stage thumbnail and display name to
        /// the UI elements. Blacks out the thumbnail when no data asset is assigned. Prefers
        /// <see cref="displayNameOverride"/> over the asset's own name when both are present.
        /// </summary>
        private void Start()
        {
            var metadata = GetComponent<UIMetadata>();

            if (!metadata.Value)
            {
                stageImage.color = Color.black;
                return;
            }

            if (metadata.Value is not StageSelectionDataSO data) return;

            stageImage.sprite = data.stageThumbnail;
            if (stageNameText) stageNameText.text = string.IsNullOrEmpty(displayNameOverride) ? data.stageDisplayName : displayNameOverride;
        }
    }
}