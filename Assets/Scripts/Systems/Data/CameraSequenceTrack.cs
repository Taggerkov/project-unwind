using UnityEngine;
using UnityEngine.Timeline;

namespace Systems.Data
{
    [System.Serializable]
    [TrackClipType(typeof(CameraKeyframeClip))]
    [TrackBindingType(typeof(Camera))]
    public class CameraSequenceTrack : TrackAsset
    {
    }
}
