using Systems.Stage;
using UnityEditor;
using UnityEngine;

namespace Dev.Common.Editor
{
    /// <summary>
    /// <see cref="CombatantSpawnMarker"/> gizmo drawer. Draws a pair of simple 3D gizmo to visualize position and orientation in editor-time.
    /// </summary>
    public abstract class CombatantSpawnMarkerGizmo
    {
        /// <summary>
        /// Draws XYZ axis rays (red/green/blue) and a yellow label at each spawn point when the
        /// <see cref="CombatantSpawnMarker"/> is not selected in the hierarchy.
        /// </summary>
        [DrawGizmo(GizmoType.NonSelected)]
        private static void DrawPositionMarkerGizmo(CombatantSpawnMarker markers, GizmoType gizmoType)
        {
            if (!markers.enabled) return;

            Gizmos.color = Color.red;
            Gizmos.DrawRay(markers.Combatant0SpawnPoint.position, markers.Combatant0SpawnPoint.transform.right);
            Gizmos.DrawRay(markers.Combatant1SpawnPoint.position, markers.Combatant1SpawnPoint.transform.right);
            
            Gizmos.color = Color.green;
            Gizmos.DrawRay(markers.Combatant0SpawnPoint.position, markers.Combatant0SpawnPoint.up);
            Gizmos.DrawRay(markers.Combatant1SpawnPoint.position, markers.Combatant1SpawnPoint.up);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(markers.Combatant0SpawnPoint.position, markers.Combatant0SpawnPoint.forward);
            Gizmos.DrawRay(markers.Combatant1SpawnPoint.position, markers.Combatant1SpawnPoint.forward);

            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.yellow;

            Handles.Label(markers.Combatant0SpawnPoint.position, markers.combatant0SpawnMarkerLabel, style);
            Handles.Label(markers.Combatant1SpawnPoint.position, markers.combatant1SpawnMarkerLabel, style);
        }

        /// <summary>Draws only the yellow spawn-point labels when the <see cref="CombatantSpawnMarker"/> is selected.</summary>
        [DrawGizmo(GizmoType.Selected)]
        private static void DrawPositionMarkerGizmoLabelOnly(CombatantSpawnMarker markers, GizmoType gizmoType)
        {
            if (!markers.enabled) return;

            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.yellow;

            Handles.Label(markers.Combatant0SpawnPoint.position, markers.combatant0SpawnMarkerLabel, style);
            Handles.Label(markers.Combatant1SpawnPoint.position, markers.combatant1SpawnMarkerLabel, style);
            
        }
    }
}