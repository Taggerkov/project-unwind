using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Eflatun.SceneReference;
using Systems.Combat.Combatant.Data;
using Systems.Common;
using Systems.Stage;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Systems.AsyncLoading
{
    public class AsyncLoader
    {
        public event Action<float> OnProgressUpdated;
        
        private AsyncOperationHandle<SceneInstance> _stageLoadHandle;
        private AsyncOperationHandle<GameObject> _p0LoadHandle;
        private AsyncOperationHandle<GameObject> _p1LoadHandle;
        
        public async UniTask<(CombatantDataSO, CombatantDataSO)> LoadCombatantData(CombatEncounterData encounterData)
        {
            var p0DataHandle = Addressables.LoadAssetAsync<CombatantDataSO>(encounterData.Combatant0);
            var p1DataHandle = Addressables.LoadAssetAsync<CombatantDataSO>(encounterData.Combatant1);
            
            await UniTask.WhenAll(p0DataHandle.ToUniTask(), p1DataHandle.ToUniTask());
            
            if (p0DataHandle.Status != AsyncOperationStatus.Succeeded)
            {
                throw new Exception($"Failed to load Combatant 0 data: {p0DataHandle.OperationException}");
            }
            
            if (p1DataHandle.Status != AsyncOperationStatus.Succeeded)
            {
                throw new Exception($"Failed to load Combatant 1 data: {p1DataHandle.OperationException}");
            }
            
            return (p0DataHandle.Result, p1DataHandle.Result);
        }

        public async UniTask LoadBattleAssets(StageEntry sceneReference, CombatantDataSO p0Data,
            CombatantDataSO p1Data)
        {
            _stageLoadHandle =
                Addressables.LoadSceneAsync(sceneReference.sceneReference.Path, UnityEngine.SceneManagement.LoadSceneMode.Additive);
            _p0LoadHandle = Addressables.LoadAssetAsync<GameObject>(p0Data.combatantPrefabReference);
            _p1LoadHandle = Addressables.LoadAssetAsync<GameObject>(p1Data.combatantPrefabReference);
            
            while (!_stageLoadHandle.IsDone || !_p0LoadHandle.IsDone || !_p1LoadHandle.IsDone)
            {
                float totalProgress = (_stageLoadHandle.PercentComplete + _p0LoadHandle.PercentComplete +
                                       _p1LoadHandle.PercentComplete) / 3f;
                OnProgressUpdated?.Invoke(totalProgress);
                await UniTask.Yield();
            }
            
            OnProgressUpdated?.Invoke(1f);
        }

        public void UnloadAssets()
        {
            if (_stageLoadHandle.IsValid())
            {
                Addressables.UnloadSceneAsync(_stageLoadHandle, true);
            }

            if (_p0LoadHandle.IsValid())
            {
                Addressables.Release(_p0LoadHandle);
            }

            if (_p1LoadHandle.IsValid())
            {
                Addressables.Release(_p1LoadHandle);
            }
        }
    }
}