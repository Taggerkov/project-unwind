using UnityEditor;
using UnityEngine;

namespace Dev.Common.Editor
{
    /// <summary>
    /// <see cref="PositionMarker"/> gizmo drawer. Draws a simple 3D gizmo to visualize position and orientation in editor-time.
    /// </summary>
    public abstract class PositionMarkerGizmo
    {
        [DrawGizmo(GizmoType.NonSelected)]
        private static void DrawPositionMarkerGizmo(PositionMarker marker, GizmoType gizmoType)
        {
            if (!marker.enabled) return;

            Gizmos.color = Color.red;
            Gizmos.DrawRay(marker.transform.position, marker.transform.forward);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(marker.transform.position, marker.transform.up);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(marker.transform.position, marker.transform.right);

            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.yellow;

            Handles.Label(marker.transform.position, marker.label, style);
        }

        [DrawGizmo(GizmoType.Selected)]
        private static void DrawPositionMarkerGizmoLabelOnly(PositionMarker marker, GizmoType gizmoType)
        {
            if (!marker.enabled) return;

            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.yellow;

            Handles.Label(marker.transform.position, marker.label, style);
        }
    }
}