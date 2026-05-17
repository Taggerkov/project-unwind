using System;
using Systems.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.UI.CombatantSelect
{
    [RequireComponent(typeof(UIMetadata))]
    public class StageSelectionButtonBinder : MonoBehaviour
    {
        [SerializeField] private Image stageImage;

        private void OnValidate()
        {
            if (!stageImage)
            {
                throw new Exception(
                    $"StageSelectionButtonBinder on {gameObject.name} is missing a reference to stageImage.");
            }

            var metadata = GetComponent<UIMetadata>();

            if (metadata.Value && metadata.Value is not StageSelectionDataSO)
            {
                throw new Exception(
                    $"StageSelectionButtonBinder on {gameObject.name} has a UIMetadata value that is not a StageSelectionDataSO.");
            }
        }

        private void Start()
        {
            var metadata = GetComponent<UIMetadata>();

            if (!metadata.Value)
            {
                stageImage.color = Color.black;
            }
            else if (metadata.Value is StageSelectionDataSO stageSelectionDataSo)
            {
                stageImage.sprite = stageSelectionDataSo.stageThumbnail;
            }
        }
    }
}