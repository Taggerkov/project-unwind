using System.Collections.Generic;
using Unity.Mathematics.Geometry;
using UnityEngine;

namespace Systems.UI.Dev.CollisionVisualizer
{
    /// <summary>
    /// Development-only MonoBehaviour that renders hitboxes and hurtboxes as coloured 3D
    /// primitives by managing two fixed-size object pools. Callers populate the box lists each
    /// frame then call <see cref="Visualize"/> to push them to the pool.
    /// </summary>
    public class CollisionVisualizer : MonoBehaviour
    {
        /// <summary>Material applied to hurtbox pool objects.</summary>
        [SerializeField] private Material hurtboxMaterial;

        /// <summary>Material applied to hitbox pool objects.</summary>
        [SerializeField] private Material hitboxMaterial;

        /// <summary>Prefab used to instantiate each pooled box primitive.</summary>
        [SerializeField] private GameObject hitboxPrefab;

        /// <summary>Fixed-size pool of hurtbox GameObjects, sized to cover the maximum expected count per frame.</summary>
        private GameObject[] _hurtboxPool = new GameObject[100];

        /// <summary>Fixed-size pool of hitbox GameObjects, sized to cover the maximum expected count per frame.</summary>
        private GameObject[] _hitboxPool = new GameObject[100];

        /// <summary>World-space hurtboxes to display on the next <see cref="Visualize"/> call.</summary>
        private List<MinMaxAABB> _hurtboxes = new();

        /// <summary>World-space hitboxes to display on the next <see cref="Visualize"/> call.</summary>
        private List<MinMaxAABB> _hitboxes = new();

        /// <summary>Locks this transform to the world origin so pooled boxes remain in world space.</summary>
        private void OnValidate()
        {
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// Creates the HurtboxPool and HitboxPool child GameObjects and pre-instantiates
        /// all pool objects with their respective materials, initially inactive.
        /// </summary>
        private void Awake()
        {
            //Create an empty gameobject 'HurtboxPool' and 'HitboxPool' inside this gameobject to hold the pooled boxes
            var hurtboxPoolParent = new GameObject("HurtboxPool");
            hurtboxPoolParent.transform.SetParent(transform);
            var hitboxPoolParent = new GameObject("HitboxPool");
            hitboxPoolParent.transform.SetParent(transform);

            for (int i = 0; i < _hurtboxPool.Length; i++)
            {
                var go = Instantiate(hitboxPrefab, hurtboxPoolParent.transform);
                go.GetComponent<MeshRenderer>().material = hurtboxMaterial;
                go.SetActive(false);
                _hurtboxPool[i] = go;
            }

            for (int i = 0; i < _hitboxPool.Length; i++)
            {
                var go = Instantiate(hitboxPrefab, hitboxPoolParent.transform);
                go.GetComponent<MeshRenderer>().material = hitboxMaterial;
                go.SetActive(false);
                _hitboxPool[i] = go;
            }
        }

        /// <summary>
        /// Positions and activates pool objects to match the current hurtbox and hitbox lists,
        /// then deactivates any pool objects that exceed the list counts.
        /// </summary>
        public void Visualize()
        {
            for (int i = 0; i < _hurtboxes.Count; i++)
            {
                if (i >= _hurtboxPool.Length) break; // pool is capped; extra boxes are silently ignored
                var box = _hurtboxes[i];
                var go = _hurtboxPool[i];
                go.transform.position = box.Center;
                go.transform.localScale = box.HalfExtents * 2f;
                go.SetActive(true);
            }

            for (int i = 0; i < _hitboxes.Count; i++)
            {
                if (i >= _hitboxPool.Length) break; // pool is capped; extra boxes are silently ignored
                var box = _hitboxes[i];
                var go = _hitboxPool[i];
                go.transform.position = box.Center;
                go.transform.localScale = box.HalfExtents * 2f;
                go.SetActive(true);
            }

            // Deactivate any unused boxes in the pool
            for (int i = _hurtboxes.Count; i < _hurtboxPool.Length; i++)
            {
                _hurtboxPool[i].SetActive(false);
            }

            for (int i = _hitboxes.Count; i < _hitboxPool.Length; i++)
            {
                _hitboxPool[i].SetActive(false);
            }
        }

        /// <summary>Deactivates every pool object, hiding all visualized boxes without clearing the lists.</summary>
        public void Hide()
        {
            foreach (var go in _hurtboxPool)
            {
                go.SetActive(false);
            }

            foreach (var go in _hitboxPool)
            {
                go.SetActive(false);
            }
        }

        /// <summary>Adds a single hurtbox to the pending display list.</summary>
        /// <param name="hurtbox">World-space AABB of the hurtbox.</param>
        public void AddHurtbox(MinMaxAABB hurtbox)
        {
            _hurtboxes.Add(hurtbox);
        }

        /// <summary>Adds a batch of hurtboxes to the pending display list.</summary>
        /// <param name="hurtboxes">World-space AABBs of the hurtboxes to add.</param>
        public void AddHurtboxes(MinMaxAABB[] hurtboxes)
        {
            _hurtboxes.AddRange(hurtboxes);
        }

        /// <summary>Adds a single hitbox to the pending display list.</summary>
        /// <param name="hitbox">World-space AABB of the hitbox.</param>
        public void AddHitbox(MinMaxAABB hitbox)
        {
            _hitboxes.Add(hitbox);
        }

        /// <summary>Adds a batch of hitboxes to the pending display list.</summary>
        /// <param name="hitboxes">World-space AABBs of the hitboxes to add.</param>
        public void AddHitboxes(MinMaxAABB[] hitboxes)
        {
            _hitboxes.AddRange(hitboxes);
        }

        /// <summary>Clears both the hurtbox and hitbox pending lists.</summary>
        public void Clear()
        {
            _hurtboxes.Clear();
            _hitboxes.Clear();
        }

        /// <summary>Clears only the hurtbox pending list.</summary>
        public void ClearHurtboxes()
        {
            _hurtboxes.Clear();
        }

        /// <summary>Clears only the hitbox pending list.</summary>
        public void ClearHitboxes()
        {
            _hitboxes.Clear();
        }
    }
}