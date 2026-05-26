using Systems.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.UI.Dev.InputHistory
{
    /// <summary>
    /// A single row in the input history display, showing the active direction and held buttons
    /// for one compressed tick frame as icon sprites.
    /// </summary>
    public class InputHistoryEntry : VisualElement
    {
        /// <summary>Container element that holds the per-button icon images for this frame.</summary>
        private VisualElement _buttonContainer;

        /// <summary>Label displaying how many consecutive ticks this input state was held.</summary>
        private Label _frameCountLabel;

        /// <summary>
        /// Clones the row template into this element and caches the button container and frame count label.
        /// </summary>
        /// <param name="template">The UI Toolkit template to instantiate for this row.</param>
        public InputHistoryEntry(VisualTreeAsset template)
        {
            template.CloneTree(this);
            _buttonContainer = this.Q<VisualElement>("ButtonContainer");
            _frameCountLabel = this.Q<Label>("FrameCount");
        }

        /// <summary>
        /// Refreshes this row with new compressed input data, making it visible and rebuilding
        /// the direction and button icons to match the given frame.
        /// </summary>
        /// <param name="data">The compressed input frame to display.</param>
        /// <param name="icons">The sprite atlas used to resolve direction and button icons.</param>
        public void Update(InputUtils.CompressedInput data, InputIconsSo icons)
        {
            style.display = DisplayStyle.Flex;

            // 2. Set Frame Count
            _frameCountLabel.text = data.FrameCount.ToString();

            // 3. Update Buttons (clear and rebuild from compressed tick data)
            _buttonContainer.Clear();
            if (data.TickData.Direction.Current != EDirectionInput.None)
            {
                if (icons.directionalIcons.TryGetValue(data.TickData.Direction.Current, out var dirIcon))
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

        /// <summary>Creates a styled icon image from the given sprite and appends it to the button container.</summary>
        /// <param name="s">The sprite to display as an icon.</param>
        private void AddIcon(Sprite s)
        {
            var icon = new Image { sprite = s };
            icon.AddToClassList("input-icon");
            _buttonContainer.Add(icon);
        }
    }
}