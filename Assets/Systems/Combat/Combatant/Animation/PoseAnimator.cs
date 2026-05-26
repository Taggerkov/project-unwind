using System;
using System.Collections.Generic;
using UnityEngine;
using Pose = Systems.Combat.Combatant.Behaviour.Pose;

namespace Systems.Combat.Combatant.Animation
{
    /// <summary>
    /// Applies <see cref="Pose"/> frames directly to a character's bone hierarchy each tick,
    /// bypassing the Unity Animator entirely. Uses a path-keyed bone cache built at startup
    /// for O(1) lookups per bone per frame.
    /// </summary>
    public class PoseAnimator : MonoBehaviour
    {
        /// <summary>Root of the character's bone hierarchy; all bone paths are relative to this transform.</summary>
        [Tooltip("The root of the character's bone hierarchy.")] [SerializeField]
        public Transform skeletonRoot;

        /// <summary>The pose most recently applied via <see cref="ApplyPose(in Pose)"/>; provides hurtbox and hitbox data for the current tick.</summary>
        public Pose CurrentPose { get; private set; }

        /// <summary>Pre-built map from hierarchical bone path to its <see cref="Transform"/>, populated by <see cref="BuildBoneCache"/>.</summary>
        private Dictionary<string, Transform> _boneMap;

        /// <summary>Walks the skeleton hierarchy from <see cref="skeletonRoot"/> and populates the internal bone map. Call once at Awake.</summary>
        public void BuildBoneCache()
        {
            _boneMap = new Dictionary<string, Transform>();
            BuildBoneCacheRecursive(_boneMap, skeletonRoot, "");
        }

        /// <summary>Applies <paramref name="pose"/> to the skeleton and caches it as <see cref="CurrentPose"/>.</summary>
        public void ApplyPose(in Pose pose)
        {
            ApplyPose(_boneMap, pose);
            CurrentPose = pose;
        }

        /// <summary>
        /// Applies each <see cref="BoneData"/> in <paramref name="pose"/> to the matching
        /// entry in <paramref name="boneMap"/>. Bones not present in the map are silently skipped.
        /// Static overload used by editor tools that manage their own bone map.
        /// </summary>
        public static void ApplyPose(Dictionary<string, Transform> boneMap, in Pose pose)
        {
            foreach (ref readonly var bone in pose.Bones.AsSpan())
            {
                if (boneMap.TryGetValue(bone.Name, out var t))
                {
                    t.localPosition = bone.LocalPosition;
                    t.localRotation = bone.LocalRotation;
                    t.localScale = bone.LocalScale;
                }
            }
        }

        /// <summary>
        /// Recursively walks the bone hierarchy, registering each bone under its full
        /// slash-delimited path (e.g. <c>Root/Spine/Chest</c>). Returns the populated map.
        /// Static so the Scriptable Animation Editor can call it on arbitrary hierarchies.
        /// </summary>
        public static Dictionary<string, Transform> BuildBoneCacheRecursive(Dictionary<string, Transform> dictionary,
            Transform bone, string parentPath)
        {
            string bonePath = string.IsNullOrEmpty(parentPath)
                ? bone.name
                : parentPath + "/" + bone.name;

            dictionary[bonePath] = bone;

            foreach (Transform child in bone)
                BuildBoneCacheRecursive(dictionary, child, bonePath);

            return dictionary;
        }
    }
}