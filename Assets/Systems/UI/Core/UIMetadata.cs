using UnityEngine;

namespace Systems.UI.Core
{
    /// <summary>
    /// Attaches a <see cref="ScriptableObject"/> payload to a UI game object so submit handlers can
    /// read typed data without a direct field reference. Place one component per selectable and assign
    /// the matching data asset in the inspector.
    /// </summary>
    public class UIMetadata : MonoBehaviour
    {
        /// <summary>The ScriptableObject this selectable carries as its data payload.</summary>
        [SerializeField] private ScriptableObject value;

        /// <summary>The ScriptableObject this selectable carries as its data payload.</summary>
        public ScriptableObject Value => value;
    }
}