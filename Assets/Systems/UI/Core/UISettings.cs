using Systems.Audio.Shared;
using UnityEngine;

namespace Systems.UI.Core
{
    /// <summary>
    /// Configuration asset for shared UI behaviour. Holds the sound events the menu plays for
    /// navigation and confirmation, kept here so they can be authored in the inspector and injected
    /// alongside the other settings assets.
    /// </summary>
    [CreateAssetMenu(fileName = "UISettings", menuName = "Unwind/UI/UI Settings", order = 0)]
    public sealed class UISettings : ScriptableObject
    {
        /// <summary>Sound played when a controller moves the cursor.</summary>
        [SerializeField] [Tooltip("Sound played when a controller moves the cursor.")]
        private AudioEvent navigateSound;

        /// <summary>Sound played when a controller confirms a selection.</summary>
        [SerializeField] [Tooltip("Sound played when a controller confirms a selection.")]
        private AudioEvent confirmSound;

        /// <summary>Border colour for player 0's selection cursor.</summary>
        [SerializeField] [Tooltip("Border colour for player 0's selection cursor.")]
        private Color player0Colour = new(0f, 0.8f, 0.7647f, 1f);

        /// <summary>Border colour for player 1's selection cursor.</summary>
        [SerializeField] [Tooltip("Border colour for player 1's selection cursor.")]
        private Color player1Colour = new(0.8f, 0f, 0f, 1f);

        /// <summary>Sound played when a controller moves the cursor.</summary>
        public AudioEvent NavigateSound => navigateSound;

        /// <summary>Sound played when a controller confirms a selection.</summary>
        public AudioEvent ConfirmSound => confirmSound;

        /// <summary>Border colour for player 0's selection cursor.</summary>
        public Color Player0Colour => player0Colour;

        /// <summary>Border colour for player 1's selection cursor.</summary>
        public Color Player1Colour => player1Colour;
    }
}
