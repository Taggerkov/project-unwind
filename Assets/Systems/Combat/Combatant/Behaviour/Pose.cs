using System;
using Unity.Mathematics.Geometry;
using UnityEngine;

namespace Systems.Combat.Combatant.Behaviour
{

    /// <summary>
    /// Transform snapshot for a single bone, stored as local-space position, rotation, and scale.
    /// Applied each tick by <see cref="Animation.PoseAnimator"/> to drive skeletal animation.
    /// </summary>
    [Serializable]
    public struct BoneData
    {
        /// <summary>Name of the bone GameObject that this snapshot targets.</summary>
        public string Name;

        /// <summary>Local position of the bone relative to its parent.</summary>
        public Vector3 LocalPosition;

        /// <summary>Local rotation of the bone relative to its parent.</summary>
        public Quaternion LocalRotation;

        /// <summary>Local scale of the bone relative to its parent.</summary>
        public Vector3 LocalScale;
    }

    /// <summary>
    /// A single animation frame: a set of bone transforms plus optional hurtbox and hitbox
    /// volumes in character-local space. Stored in <see cref="Animation.CombatantPoseCollection"/>
    /// and driven by <see cref="MoveRunner"/> tick-by-tick.
    /// </summary>
    [Serializable]
    public struct Pose
    {
        /// <summary>Bone transform snapshots that <see cref="Animation.PoseAnimator"/> applies this frame.</summary>
        public BoneData[] Bones;

        /// <summary>Character-local AABBs defining the active hurtbox volumes for this frame.</summary>
        public MinMaxAABB[] Hurtboxes;

        /// <summary>Character-local AABBs defining the active hitbox volumes for this frame. Only registered by <see cref="CombatManager"/> during the Active phase.</summary>
        public MinMaxAABB[] Hitboxes;
    }
}
