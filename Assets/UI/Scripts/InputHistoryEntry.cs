using Systems.Input;
using UI.Icons;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Scripts
{
    public class InputHistoryEntry : VisualElement
    {
        private Image _directionIcon;
        private VisualElement _buttonContainer;
        private Label _frameCountLabel;

        public InputHistoryEntry(VisualTreeAsset template)
        {
            template.CloneTree(this);
            _directionIcon = this.Q<Image>("DirectionIcon");
            _buttonContainer = this.Q<VisualElement>("ButtonContainer");
            _frameCountLabel = this.Q<Label>("FrameCount");
        }

        public void Update(InputUtils.CompressedInput data, InputIconsSo icons)
        {
            style.display = DisplayStyle.Flex;
        
            // 1. Set Direction
            _directionIcon.sprite = icons.directionalIcons[InputUtils.NumpadToInputType(data.FrameData.Direction)];
        
            // 2. Set Frame Count (Only show if > 1 for clean look)
            _frameCountLabel.text = data.FrameCount > 1 ? data.FrameCount.ToString() : "";

            // 3. Update Buttons (Clear and add, or toggle visibility of pre-existing icons)
            _buttonContainer.Clear();
            if (data.FrameData.LightAttack.Held) AddIcon(icons.lightAttack);
            if (data.FrameData.MediumAttack.Held) AddIcon(icons.mediumAttack);
            if (data.FrameData.HeavyAttack.Held) AddIcon(icons.heavyAttack);
            if (data.FrameData.UniqueAttack.Held) AddIcon(icons.uniqueAttack);
            if (data.FrameData.GuardButton.Held) AddIcon(icons.guard);
            if (data.FrameData.AbilityButton.Held) AddIcon(icons.ability);
        }

        private void AddIcon(Sprite s) => _buttonContainer.Add(new Image { sprite = s });
    }
}