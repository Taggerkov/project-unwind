using System;
using Cysharp.Threading.Tasks;
using Eflatun.SceneReference;
using FixedLogic;
using Reflex.Attributes;
using Systems.AsyncLoading;
using Systems.Combat.Combatant.Data;
using Systems.Core;
using Systems.Stage;
using UnityEngine;

namespace Systems.Dev
{
    public class TestingScript : MonoBehaviour
    {
        [SerializeField] private SceneReference sceneToLoad;
        [SerializeField] private CombatantDataSO combatantData0;
        [SerializeField] private CombatantDataSO combatantData1;
        [Inject] private readonly AsyncLoader _asyncLoader;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Awake()
        {
            StageEntry entry = new StageEntry();
            entry.sceneReference = sceneToLoad;
            // _asyncLoader.BeginCharacterSelect();
            _asyncLoader.LoadBattleAssets(entry, combatantData0, combatantData1).Forget();
        }

        public void LogicTick()
        {
            Debug.Log("LogicTick from TestingScript");
        }

        private void OnEnable()
        {
            _asyncLoader.OnProgressUpdated += HandleProgressUpdated;
        }
        
        private void OnDisable()
        {
            _asyncLoader.OnProgressUpdated -= HandleProgressUpdated;
        }
        
        private void HandleProgressUpdated(float progress)
        {
            Debug.Log($"Loading progress: {progress * 100f}%");
        }
    }
}