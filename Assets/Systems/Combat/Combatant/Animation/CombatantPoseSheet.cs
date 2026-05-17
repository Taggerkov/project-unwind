using AYellowpaper.SerializedCollections;
using Systems.Combat.HitSystem;
using UnityEngine;
using Pose = Systems.Combat.Combatant.Behaviour.Pose;

namespace Systems.Combat.Combatant.Animation
{
    [CreateAssetMenu(fileName = "CombatantPoseSheet", menuName = "Unwind Database/Combatant/Combatant PoseSheet")]
    public class CombatantPoseSheet : ScriptableObject
    {
        // ── Standard pose registry ─────────────────────────────────────────────────
        // User-facing collections. Keep collection IDs below 50 to avoid the
        // reserved damage/block range (collections 50–59, global IDs 5000–5999).
        [SerializeField] private SerializedDictionary<uint, CombatantPoseCollection> _poses = new();

        // ── Damage pose collections (reserved collection IDs 50–54) ───────────────
        // One collection per hit level. Each collection may contain multiple poses
        // (e.g. impact frame at index 0, tumble frame at index 1).
        // Global IDs: GetDamagePoseGlobalId(level, poseIndex).
        [Header("Damage Poses (Hit Levels 1–5)")] [SerializeField]
        private CombatantPoseCollection _level1DamagePoses;

        [SerializeField] private CombatantPoseCollection _level2DamagePoses;
        [SerializeField] private CombatantPoseCollection _level3DamagePoses;
        [SerializeField] private CombatantPoseCollection _level4DamagePoses;
        [SerializeField] private CombatantPoseCollection _level5DamagePoses;

        // ── Block pose collections (reserved collection IDs 55–59) ────────────────
        [Header("Block Poses (Hit Levels 1–5)")] [SerializeField]
        private CombatantPoseCollection _level1BlockPoses;

        [SerializeField] private CombatantPoseCollection _level2BlockPoses;
        [SerializeField] private CombatantPoseCollection _level3BlockPoses;
        [SerializeField] private CombatantPoseCollection _level4BlockPoses;
        [SerializeField] private CombatantPoseCollection _level5BlockPoses;

        // ── Reserved collection ID constants ──────────────────────────────────────
        // Damage: collections 50–54  →  global IDs 5000–5499
        // Block:  collections 55–59  →  global IDs 5500–5999
        private const uint DamageCollectionBase = 50;
        private const uint BlockCollectionBase = 55;

        // ── Global ID helpers (used by CmnActHitstun / CmnActBlockstun) ───────────

        /// <summary>
        /// Returns the global pose ID for a damage pose at the given hit level and
        /// index within its collection.  EHitLevel.One = collection 50, etc.
        /// </summary>
        public static uint GetDamagePoseGlobalId(EHitLevel level, uint poseIndex = 0)
            => (DamageCollectionBase + (uint)level) * 100 + poseIndex;

        /// <summary>
        /// Returns the global pose ID for a block pose at the given hit level and
        /// index within its collection.  EHitLevel.One = collection 55, etc.
        /// </summary>
        public static uint GetBlockPoseGlobalId(EHitLevel level, uint poseIndex = 0)
            => (BlockCollectionBase + (uint)level) * 100 + poseIndex;

        // ── Unified pose lookup ────────────────────────────────────────────────────

        public bool TryGetPose(uint collectionId, uint poseId, out Pose pose)
        {
            // Damage pose range
            if (collectionId >= DamageCollectionBase && collectionId < DamageCollectionBase + 5)
            {
                var collection = GetDamagePoseCollection((int)(collectionId - DamageCollectionBase));
                if (collection != null && collection.poses.TryGetValue(poseId, out pose)) return true;
                pose = default;
                return false;
            }

            // Block pose range
            if (collectionId >= BlockCollectionBase && collectionId < BlockCollectionBase + 5)
            {
                var collection = GetBlockPoseCollection((int)(collectionId - BlockCollectionBase));
                if (collection != null && collection.poses.TryGetValue(poseId, out pose)) return true;
                pose = default;
                return false;
            }

            // Standard registry
            if (_poses.TryGetValue(collectionId, out var standard))
                return standard.poses.TryGetValue(poseId, out pose);

            pose = default;
            return false;
        }

        private CombatantPoseCollection GetDamagePoseCollection(int index) => index switch
        {
            0 => _level1DamagePoses,
            1 => _level2DamagePoses,
            2 => _level3DamagePoses,
            3 => _level4DamagePoses,
            4 => _level5DamagePoses,
            _ => null
        };

        private CombatantPoseCollection GetBlockPoseCollection(int index) => index switch
        {
            0 => _level1BlockPoses,
            1 => _level2BlockPoses,
            2 => _level3BlockPoses,
            3 => _level4BlockPoses,
            4 => _level5BlockPoses,
            _ => null
        };

#if UNITY_EDITOR
        public void EditorAddOrReplace(uint collectionId, uint poseId, Pose pose)
        {
            _poses.TryGetValue(collectionId, out var collection);
            if (!collection)
            {
                var beginningRange = (collectionId * 100).ToString("D3");
                var endingRange = (collectionId * 100 + 99).ToString("D3");
                collection = CreateInstance<CombatantPoseCollection>();
                string path = UnityEditor.AssetDatabase.GetAssetPath(this);
                string folder = System.IO.Path.GetDirectoryName(path);
                folder = System.IO.Path.Combine(folder, "Poses");

                if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                    UnityEditor.AssetDatabase.CreateFolder(
                        System.IO.Path.GetDirectoryName(folder),
                        System.IO.Path.GetFileName(folder));

                folder = System.IO.Path.Combine(folder, beginningRange);
                if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                    UnityEditor.AssetDatabase.CreateFolder(
                        System.IO.Path.GetDirectoryName(folder),
                        System.IO.Path.GetFileName(folder));

                string collectionPath = System.IO.Path.Combine(
                    folder, $"PoseCollection_{beginningRange}-{endingRange}.asset");
                UnityEditor.AssetDatabase.CreateAsset(collection, collectionPath);
                _poses[collectionId] = collection;
            }

            _poses[collectionId].poses[poseId] = pose;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(collection);
        }

        public void EditorRemove(uint collectionId, uint poseId)
        {
            if (_poses.TryGetValue(collectionId, out var collection))
                if (collection.poses.Remove(poseId))
                    UnityEditor.EditorUtility.SetDirty(this);
        }

        public bool EditorHasId(uint collectionId, uint poseId)
        {
            if (_poses.TryGetValue(collectionId, out var collection))
                return collection.poses.ContainsKey(poseId);
            return false;
        }
#endif
    }
}