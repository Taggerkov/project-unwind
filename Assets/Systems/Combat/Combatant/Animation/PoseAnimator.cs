using System;
using System.Collections.Generic;
using UnityEngine;
using Pose = Systems.Combat.Combatant.Behaviour.Pose;

namespace Systems.Combat.Combatant.Animation
{
    public class PoseAnimator : MonoBehaviour
    {
        [Tooltip("The root of the character's bone hierarchy.")] [SerializeField]
        public Transform skeletonRoot;

        public Pose CurrentPose { get; private set; }

        private Dictionary<string, Transform> _boneMap;

        public void BuildBoneCache()
        {
            _boneMap = new Dictionary<string, Transform>();
            BuildBoneCacheRecursive(_boneMap, skeletonRoot, "");
        }

        public void ApplyPose(in Pose pose)
        {
            ApplyPose(_boneMap, pose);
            CurrentPose = pose;
        }

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