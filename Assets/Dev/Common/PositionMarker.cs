using UnityEngine;

namespace Dev.Common
{
    /// <summary>
    /// Simple helper to visualize position and orientation in editor-time through a 3D gizmo.
    /// </summary>
    public class PositionMarker : MonoBehaviour
    {
        public string label;
        
        
        private void Reset()
        {
            label = gameObject.name;
        }
    }
}