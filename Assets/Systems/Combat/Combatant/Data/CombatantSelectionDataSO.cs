using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Systems.Combat.Combatant.Data
{
    /// <summary>
    /// Lightweight asset used by the character selection UI. Holds only the data needed to
    /// render a character card; the full <see cref="CombatantDataSO"/> is loaded on demand
    /// via <see cref="combatantDataReference"/> when the character is confirmed.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatantSelectionDataSO", menuName = "Unwind Database/Combatant Selection Data")]
    public class CombatantSelectionDataSO : ScriptableObject
    {
        /// <summary>Name displayed on the character selection card.</summary>
        public string combatantDisplayName;

        /// <summary>Portrait sprite shown on the character selection card.</summary>
        public Sprite combatantThumbnail;

        /// <summary>Addressable reference to the full combatant data asset, loaded when the character is selected.</summary>
        public AssetReferenceT<CombatantDataSO> combatantDataReference;
    }
}
