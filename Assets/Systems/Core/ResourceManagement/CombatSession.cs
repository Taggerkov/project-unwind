using System;
using Cysharp.Threading.Tasks;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.Combatant.Data;
using Systems.Common;
using Systems.Stage;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Systems.Core.ResourceManagement
{
    public class CombatSession : IDisposable
    {
        private AsyncOperationHandle<SceneInstance> _stageHandle;
        private AsyncOperationHandle<GameObject> _c0InstanceHandle;
        private AsyncOperationHandle<GameObject> _c1InstanceHandle;

        // Keep data handles alive so the SOs remain valid for the session's lifetime
        private AsyncOperationHandle<StageEntrySO> _stageDataHandle;
        private AsyncOperationHandle<CombatantDataSO> _c0DataHandle;
        private AsyncOperationHandle<CombatantDataSO> _c1DataHandle;

        public CombatantBehaviour Combatant0 { get; private set; }
        
        public CombatantDataSO Combatant0Data { get; private set; }
        public CombatantBehaviour Combatant1 { get; private set; }
        
        public CombatantDataSO Combatant1Data { get; private set; }
        public StageEntrySO StageData { get; private set; }

        public static async UniTask<CombatSession> LoadAsync(
            CombatEncounterData encounterData,
            Action<float> onProgress = null)
        {
            var session = new CombatSession();
            await session.LoadInternalAsync(encounterData, onProgress);
            return session;
        }

        private async UniTask LoadInternalAsync(CombatEncounterData data, Action<float> onProgress)
        {
            _stageDataHandle = Addressables.LoadAssetAsync<StageEntrySO>(data.Stage);
            _c0DataHandle = Addressables.LoadAssetAsync<CombatantDataSO>(data.Combatant0);
            _c1DataHandle = Addressables.LoadAssetAsync<CombatantDataSO>(data.Combatant1);

            await UniTask.WhenAll(
                _stageDataHandle.ToUniTask(),
                _c0DataHandle.ToUniTask(),
                _c1DataHandle.ToUniTask());

            StageData = _stageDataHandle.Result;
            Combatant0Data = _c0DataHandle.Result;
            Combatant1Data = _c1DataHandle.Result;

            // Data handles stay alive. Now safe to use c0Data and c1Data.
            _stageHandle = Addressables.LoadSceneAsync(StageData.sceneReference.Path,
                LoadSceneMode.Additive, activateOnLoad: false);
            _c0InstanceHandle = Combatant0Data.combatantPrefabReference.InstantiateAsync();
            _c1InstanceHandle = Combatant1Data.combatantPrefabReference.InstantiateAsync();

            while (!_stageHandle.IsDone || !_c0InstanceHandle.IsDone || !_c1InstanceHandle.IsDone)
            {
                float progress = (_stageHandle.PercentComplete +
                                  _c0InstanceHandle.PercentComplete +
                                  _c1InstanceHandle.PercentComplete) / 3f;
                onProgress?.Invoke(progress);
                await UniTask.Yield();
            }

            onProgress?.Invoke(1f);

            Combatant0 = _c0InstanceHandle.Result.GetComponent<CombatantBehaviour>();
            Combatant1 = _c1InstanceHandle.Result.GetComponent<CombatantBehaviour>();
        }

        public async UniTask ActivateSceneAsync()
        {
            await _stageHandle.Result.ActivateAsync().ToUniTask();
        }

        public async UniTask DisposeAsync()
        {
            if (_c0InstanceHandle.IsValid()) Addressables.ReleaseInstance(_c0InstanceHandle);
            if (_c1InstanceHandle.IsValid()) Addressables.ReleaseInstance(_c1InstanceHandle);
            if (_stageHandle.IsValid()) await Addressables.UnloadSceneAsync(_stageHandle).ToUniTask();

            // Now release data handles too
            if (_stageDataHandle.IsValid()) Addressables.Release(_stageDataHandle);
            if (_c0DataHandle.IsValid()) Addressables.Release(_c0DataHandle);
            if (_c1DataHandle.IsValid()) Addressables.Release(_c1DataHandle);

            _c0InstanceHandle = default;
            _c1InstanceHandle = default;
            _stageHandle = default;
            _stageDataHandle = default;
            _c0DataHandle = default;
            _c1DataHandle = default;
        }

        void IDisposable.Dispose() => DisposeAsync().Forget();
    }
}