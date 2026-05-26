using AYellowpaper.SerializedCollections;
using Systems.Input;
using UnityEngine;

namespace Systems.UI.Dev.InputHistory
{
    /// <summary>
    /// Configuration asset mapping each input action to a display sprite for the input history overlay.
    /// Create via <c>Create → UI → Input Icons</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "InputIcons", menuName = "UI/Input Icons")]
    public class InputIconsSo : ScriptableObject
    {
        /// <summary>Maps each numpad direction to its corresponding icon sprite.</summary>
        public SerializedDictionary<EDirectionInput, Sprite> directionalIcons;

        /// <summary>Icon sprite for the light attack button.</summary>
        public Sprite lightAttack;

        /// <summary>Icon sprite for the medium attack button.</summary>
        public Sprite mediumAttack;

        /// <summary>Icon sprite for the heavy attack button.</summary>
        public Sprite heavyAttack;

        /// <summary>Icon sprite for the unique attack button.</summary>
        public Sprite uniqueAttack;

        /// <summary>Icon sprite for the guard button.</summary>
        public Sprite guard;

        /// <summary>Icon sprite for the ability button.</summary>
        public Sprite ability;
    }
}
