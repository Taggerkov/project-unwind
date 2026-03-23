using System;
using AYellowpaper.SerializedCollections;
using Eflatun.SceneReference;
using UnityEngine;

namespace Systems.Stage
{
    public enum ECombatEncounterStage
    {
        RuinedCathedral
    }
    
    [Serializable]
    public struct StageEntry
    {
        public SceneReference sceneReference;
    }

    [CreateAssetMenu(fileName = "StageDatabase", menuName = "Unwind Database/Stage Database")]
    public class StageDatabaseSO : ScriptableObject
    {
        public SerializedDictionary<ECombatEncounterStage, StageEntry> stageEntries;
    }
}
