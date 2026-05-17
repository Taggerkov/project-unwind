using System.Collections.Generic;
using Unity.Mathematics.Geometry;
using UnityEngine;

namespace Systems.UI.Dev.CollisionVisualizer
{
    public class CollisionVisualizer : MonoBehaviour
    {
        [SerializeField] private Material hurtboxMaterial;
        [SerializeField] private Material hitboxMaterial;

        [SerializeField] private GameObject hitboxPrefab;

        private GameObject[] _hurtboxPool = new GameObject[100];
        private GameObject[] _hitboxPool = new GameObject[100];

        /// <summary>
        /// A list of hurtboxes in world space being currently displayed.
        /// </summary>
        private List<MinMaxAABB> _hurtboxes = new();

        /// <summary>
        /// A list of hitboxes in world space being currently displayed.
        /// </summary>
        private List<MinMaxAABB> _hitboxes = new();

        private void OnValidate()
        {
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

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

        public void Visualize()
        {
            for (int i = 0; i < _hurtboxes.Count; i++)
            {
                if (i >= _hurtboxPool.Length) break; // safety check to avoid out of bounds
                var box = _hurtboxes[i];
                var go = _hurtboxPool[i];
                go.transform.position = box.Center;
                go.transform.localScale = box.HalfExtents * 2f;
                go.SetActive(true);
            }

            for (int i = 0; i < _hitboxes.Count; i++)
            {
                if (i >= _hitboxPool.Length) break; // safety check to avoid out of bounds
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

        public void AddHurtbox(MinMaxAABB hurtbox)
        {
            _hurtboxes.Add(hurtbox);
        }

        public void AddHurtboxes(MinMaxAABB[] hurtboxes)
        {
            _hurtboxes.AddRange(hurtboxes);
        }

        public void AddHitbox(MinMaxAABB hitbox)
        {
            _hitboxes.Add(hitbox);
        }

        public void AddHitboxes(MinMaxAABB[] hitboxes)
        {
            _hitboxes.AddRange(hitboxes);
        }

        public void Clear()
        {
            _hurtboxes.Clear();
            _hitboxes.Clear();
        }

        public void ClearHurtboxes()
        {
            _hurtboxes.Clear();
        }

        public void ClearHitboxes()
        {
            _hitboxes.Clear();
        }
    }
}