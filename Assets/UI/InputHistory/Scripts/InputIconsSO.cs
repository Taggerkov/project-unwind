using AYellowpaper.SerializedCollections;
using Systems.Combat.Core.Input;
using UnityEngine;

namespace UI.Icons
{
    [CreateAssetMenu(fileName = "InputIcons", menuName = "UI/Input Icons")]
    public class InputIconsSo : ScriptableObject
    {
        public SerializedDictionary<EInputType, Sprite> directionalIcons;
        public Sprite lightAttack;
        public Sprite mediumAttack;
        public Sprite heavyAttack;
        public Sprite uniqueAttack;
        public Sprite guard;
        public Sprite ability;
    }
}
