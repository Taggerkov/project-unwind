using UnityEngine;

namespace Systems.UI.Common
{
    public class UIMetadata : MonoBehaviour
    {
        [SerializeField] private ScriptableObject value;
        
        public ScriptableObject Value => value;
    }
}