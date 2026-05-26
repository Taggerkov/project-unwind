using AYellowpaper.SerializedCollections;
using Systems.Combat.HitSystem;
using UnityEngine;
using Pose = Systems.Combat.Combatant.Behaviour.Pose;

namespace Systems.Combat.Combatant.Animation
{
    /// <summary>
    /// ScriptableObject that organises all of a character's <see cref="CombatantPoseCollection"/>
    /// assets into a unified lookup. Standard move poses use collection IDs 0–49; reserved ranges
    /// (50–54 damage, 55–59 block) map to the per-hit-level pose arrays. Provides helper methods
    /// to derive global IDs used by hitstun and blockstun move scripts.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatantPoseSheet", menuName = "Unwind Database/Combatant/Combatant PoseSheet")]
    public class CombatantPoseSheet : ScriptableObject
    {
        // ── Standard pose registry ─────────────────────────────────────────────────
        // User-facing collections. Keep collection IDs below 50 to avoid the
        // reserved damage/block range (collections 50–59, global IDs 5000–5999).
        /// <summary>Standard move pose collections keyed by collection ID (0–49).</summary>
        [SerializeField] private SerializedDictionary<uint, CombatantPoseCollection> _poses = new();

        /// <summary>Fallback pose returned when a requested ID is not found in any collection.</summary>
        [SerializeField] private Pose _defaultPose;

        /// <summary>Whether a default pose has been configured; serialised so the editor can show a warning when missing.</summary>
        [SerializeField, HideInInspector] private bool _hasDefaultPose;

        // ── Damage pose collections (reserved collection IDs 50–54) ───────────────
        // One collection per hit level. Each collection may contain multiple poses
        // (e.g. impact frame at index 0, tumble frame at index 1).
        // Global IDs: GetDamagePoseGlobalId(level, poseIndex).
        /// <summary>Damage poses for hit level 1 (reserved collection ID 50, global IDs 5000–5099).</summary>
        [Header("Damage Poses (Hit Levels 1–5)")] [SerializeField]
        private CombatantPoseCollection _level1DamagePoses;

        /// <summary>Damage poses for hit level 2 (reserved collection ID 51).</summary>
        [SerializeField] private CombatantPoseCollection _level2DamagePoses;

        /// <summary>Damage poses for hit level 3 (reserved collection ID 52).</summary>
        [SerializeField] private CombatantPoseCollection _level3DamagePoses;

        /// <summary>Damage poses for hit level 4 (reserved collection ID 53).</summary>
        [SerializeField] private CombatantPoseCollection _level4DamagePoses;

        /// <summary>Damage poses for hit level 5 (reserved collection ID 54).</summary>
        [SerializeField] private CombatantPoseCollection _level5DamagePoses;

        // ── Block pose collections (reserved collection IDs 55–59) ────────────────
        /// <summary>Block poses for hit level 1 (reserved collection ID 55, global IDs 5500–5599).</summary>
        [Header("Block Poses (Hit Levels 1–5)")] [SerializeField]
        private CombatantPoseCollection _level1BlockPoses;

        /// <summary>Block poses for hit level 2 (reserved collection ID 56).</summary>
        [SerializeField] private CombatantPoseCollection _level2BlockPoses;

        /// <summary>Block poses for hit level 3 (reserved collection ID 57).</summary>
        [SerializeField] private CombatantPoseCollection _level3BlockPoses;

        /// <summary>Block poses for hit level 4 (reserved collection ID 58).</summary>
        [SerializeField] private CombatantPoseCollection _level4BlockPoses;

        /// <summary>Block poses for hit level 5 (reserved collection ID 59).</summary>
        [SerializeField] private CombatantPoseCollection _level5BlockPoses;

        // ── Reserved collection ID constants ──────────────────────────────────────
        // Damage: collections 50–54  →  global IDs 5000–5499
        // Block:  collections 55–59  →  global IDs 5500–5999
        /// <summary>First collection ID reserved for damage poses.</summary>
        private const uint DamageCollectionBase = 50;

        /// <summary>First collection ID reserved for block poses.</summary>
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

        /// <summary>
        /// Looks up a pose by collection ID and local pose ID. Checks damage, block, and standard
        /// registries in order. Returns true when found; otherwise outputs <see cref="_defaultPose"/> and returns false.
        /// </summary>
        public bool TryGetPose(uint collectionId, uint poseId, out Pose pose)
        {
            // Damage pose range
            if (collectionId >= DamageCollectionBase && collectionId < DamageCollectionBase + 5)
            {
                var collection = GetDamagePoseCollection((int)(collectionId - DamageCollectionBase));
                if (collection != null && collection.poses.TryGetValue(poseId, out pose)) return true;
                pose = _defaultPose;
                return false;
            }

            // Block pose range
            if (collectionId >= BlockCollectionBase && collectionId < BlockCollectionBase + 5)
            {
                var collection = GetBlockPoseCollection((int)(collectionId - BlockCollectionBase));
                if (collection != null && collection.poses.TryGetValue(poseId, out pose)) return true;
                pose = _defaultPose;
                return false;
            }

            // Standard registry
            if (_poses.TryGetValue(collectionId, out var standard))
                return standard.poses.TryGetValue(poseId, out pose);

            pose = _defaultPose;
            return false;
        }

        /// <summary>Returns the damage pose collection at the given 0-based level index (0 = level 1, …, 4 = level 5).</summary>
        private CombatantPoseCollection GetDamagePoseCollection(int index) => index switch
        {
            0 => _level1DamagePoses,
            1 => _level2DamagePoses,
            2 => _level3DamagePoses,
            3 => _level4DamagePoses,
            4 => _level5DamagePoses,
            _ => null
        };

        /// <summary>Returns the block pose collection at the given 0-based level index (0 = level 1, …, 4 = level 5).</summary>
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
        /// <summary>
        /// Adds or replaces a pose in the standard collection at <paramref name="collectionId"/>/<paramref name="poseId"/>.
        /// Creates the collection ScriptableObject and its folder hierarchy when missing.
        /// Editor-only; compiled out in player builds.
        /// </summary>
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

        /// <summary>Removes the pose at <paramref name="collectionId"/>/<paramref name="poseId"/> from the standard registry. Editor-only.</summary>
        public void EditorRemove(uint collectionId, uint poseId)
        {
            if (_poses.TryGetValue(collectionId, out var collection))
                if (collection.poses.Remove(poseId))
                    UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>Returns true when a pose exists at <paramref name="collectionId"/>/<paramref name="poseId"/>. Editor-only.</summary>
        public bool EditorHasId(uint collectionId, uint poseId)
        {
            if (_poses.TryGetValue(collectionId, out var collection))
                return collection.poses.ContainsKey(poseId);
            return false;
        }

        /// <summary>Returns the current default pose. Editor-only.</summary>
        public Pose GetDefaultPose() => _defaultPose;

        /// <summary>Assigns the default fallback pose and marks the asset dirty. Editor-only.</summary>
        public void SetDefaultPose(Pose pose)
        {
            _defaultPose = pose;
            _hasDefaultPose = true;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>Returns true when a default pose has been configured. Editor-only.</summary>
        public bool HasDefaultPose() => _hasDefaultPose;
#endif
    }
}