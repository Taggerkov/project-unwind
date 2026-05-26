using System;
using AYellowpaper.SerializedCollections;
using Eflatun.SceneReference;
using UnityEngine;

namespace Systems.Stage
{
    /// <summary>
    /// ScriptableObject that represents a single playable stage. Holds the Addressable scene reference
    /// used by <c>GameManager</c> to load the stage during match setup.
    /// Create via <c>Unwind Database → Stage → Stage Entry</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "StageDatabase", menuName = "Unwind Database/Stage/Stage Entry")]
    public class StageEntrySO : ScriptableObject
    {
        /// <summary>Scene that contains the stage environment and <see cref="CombatantSpawnMarker"/>.</summary>
        public SceneReference sceneReference;
    }
}
