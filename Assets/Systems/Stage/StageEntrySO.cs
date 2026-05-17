using System;
using AYellowpaper.SerializedCollections;
using Eflatun.SceneReference;
using UnityEngine;

namespace Systems.Stage
{
    [CreateAssetMenu(fileName = "StageDatabase", menuName = "Unwind Database/Stage/Stage Entry")]
    public class StageEntrySO : ScriptableObject
    {
        public SceneReference sceneReference;

    }
}
