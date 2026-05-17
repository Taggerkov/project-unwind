using System;
using Unity.Mathematics.Geometry;
using UnityEngine;

namespace Systems.Combat.Combatant.Behaviour
{

    [Serializable]
    public struct BoneData
    {
        public string Name;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
    }

    [Serializable]
    public struct Pose
    {
        public BoneData[] Bones;
        public MinMaxAABB[] Hurtboxes;
        public MinMaxAABB[] Hitboxes;
    }
}
