using Systems.Combat.Core.Input;
using Systems.Input;
using UI.Icons;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Scripts
{
    public class InputHistoryEntry : VisualElement
    {
        private VisualElement _buttonContainer;
        private Label _frameCountLabel;

        public InputHistoryEntry(VisualTreeAsset template)
        {
            template.CloneTree(this);
            _buttonContainer = this.Q<VisualElement>("ButtonContainer");
            _frameCountLabel = this.Q<Label>("FrameCount");
        }

        public void Update(InputUtils.CompressedInput data, InputIconsSo icons)
        {
            style.display = DisplayStyle.Flex;

            // 2. Set Frame Count 
            _frameCountLabel.text = data.FrameCount.ToString();

            // 3. Update Buttons (Clear and add, or toggle visibility of pre-existing icons)
            _buttonContainer.Clear();
            if (data.TickData.Direction != EInputType.Input5) // Only show directional icon if not neutral
            {
                if (icons.directionalIcons.TryGetValue(data.TickData.Direction, out var dirIcon))
                {
                    AddIcon(dirIcon);
                }
            }

            if (data.TickData.LightAttack.Held) AddIcon(icons.lightAttack);
            if (data.TickData.MediumAttack.Held) AddIcon(icons.mediumAttack);
            if (data.TickData.HeavyAttack.Held) AddIcon(icons.heavyAttack);
            if (data.TickData.UniqueAttack.Held) AddIcon(icons.uniqueAttack);
            if (data.TickData.GuardButton.Held) AddIcon(icons.guard);
            if (data.TickData.AbilityButton.Held) AddIcon(icons.ability);
        }

        private void AddIcon(Sprite s)
        {
            //Add class "input-icon" to the image for consistent sizing
            var icon = new Image { sprite = s };
            icon.AddToClassList("input-icon");
            _buttonContainer.Add(icon);
        }
    }
}