using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Systems.Combat.Combatant.Data
{
    [CreateAssetMenu(fileName = "CombatantSelectionDataSO", menuName = "Unwind Database/Combatant Selection Data")]
    public class CombatantSelectionDataSO : ScriptableObject
    {
        public string combatantDisplayName;
        public Sprite combatantThumbnail;
        

        //Addressable reference to the scriptable object
        public AssetReferenceT<CombatantDataSO> combatantDataReference;
    }
}
