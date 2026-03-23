using UnityEngine;

namespace Systems.Data
{
    [CreateAssetMenu(fileName = "ScriptableAnimationSO", menuName = "Unwind Database/Scriptable Animation", order = 1)]
    public class ScriptableAnimationSO : ScriptableObject
    {
        public string animationName;

        public FramePose[] frames;
    }
}
