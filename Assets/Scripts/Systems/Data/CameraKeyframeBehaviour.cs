using UnityEngine;
using UnityEngine.Playables;

// A behaviour that is attached to a playable
namespace Systems.Data
{
    public class CameraKeyframeBehaviour : PlayableBehaviour
    {

        public CameraKeyframe KeyframeData;

        private Camera _targetCamera;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            _targetCamera = playerData as Camera;
            if (!_targetCamera) return;

            _targetCamera.transform.position = KeyframeData.localPosition;
            _targetCamera.fieldOfView = KeyframeData.fieldOfView;
        }
    }
}
