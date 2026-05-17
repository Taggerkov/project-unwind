using AYellowpaper.SerializedCollections;
using Systems.Input;
using UnityEngine;

namespace Systems.UI.Dev.InputHistory.Scripts
{
    [CreateAssetMenu(fileName = "InputIcons", menuName = "UI/Input Icons")]
    public class InputIconsSo : ScriptableObject
    {
        public SerializedDictionary<EDirectionInput, Sprite> directionalIcons;
        public Sprite lightAttack;
        public Sprite mediumAttack;
        public Sprite heavyAttack;
        public Sprite uniqueAttack;
        public Sprite guard;
        public Sprite ability;
    }
}
