using UnityEngine;
using UnityEngine.Playables;

namespace Systems.Data
{
    [System.Serializable]
    public class CameraKeyframeClip : PlayableAsset
    {
        // This exposes your struct in the Inspector when you click the clip!
        public CameraKeyframe keyframeData; 

        // Unity calls this to generate the underlying Playable logic
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<CameraKeyframeBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
        
            // Pass the Inspector data into the logic script
            behaviour.KeyframeData = keyframeData;
        
            return playable;
        }
    }
}
