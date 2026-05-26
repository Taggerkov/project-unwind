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
    /// <summary>
    /// Manages the full Addressables lifetime of one combat encounter: loads the stage scene,
    /// both combatant prefabs, and their data assets, then releases them when the session is
    /// disposed. Create via <see cref="LoadAsync"/>; dispose via <see cref="DisposeAsync"/>.
    /// </summary>
    public class CombatSession : IDisposable
    {
        /// <summary>Addressable handle for the loaded stage scene; kept alive until <see cref="DisposeAsync"/>.</summary>
        private AsyncOperationHandle<SceneInstance> _stageHandle;

        /// <summary>Addressable handle for the instantiated combatant 0 prefab.</summary>
        private AsyncOperationHandle<GameObject> _c0InstanceHandle;

        /// <summary>Addressable handle for the instantiated combatant 1 prefab.</summary>
        private AsyncOperationHandle<GameObject> _c1InstanceHandle;

        // Keep data handles alive so the SOs remain valid for the session's lifetime
        /// <summary>Addressable handle keeping the <see cref="StageEntrySO"/> asset loaded for the session duration.</summary>
        private AsyncOperationHandle<StageEntrySO> _stageDataHandle;

        /// <summary>Addressable handle keeping the combatant 0 data asset loaded for the session duration.</summary>
        private AsyncOperationHandle<CombatantDataSO> _c0DataHandle;

        /// <summary>Addressable handle keeping the combatant 1 data asset loaded for the session duration.</summary>
        private AsyncOperationHandle<CombatantDataSO> _c1DataHandle;

        /// <summary>The instantiated combatant 0 behaviour component.</summary>
        public CombatantBehaviour Combatant0 { get; private set; }

        /// <summary>The loaded data asset for combatant 0.</summary>
        public CombatantDataSO Combatant0Data { get; private set; }

        /// <summary>The instantiated combatant 1 behaviour component.</summary>
        public CombatantBehaviour Combatant1 { get; private set; }

        /// <summary>The loaded data asset for combatant 1.</summary>
        public CombatantDataSO Combatant1Data { get; private set; }

        /// <summary>The loaded stage entry data, including the scene reference.</summary>
        public StageEntrySO StageData { get; private set; }

        /// <summary>
        /// Creates and fully loads a <see cref="CombatSession"/> for the given encounter.
        /// </summary>
        /// <param name="encounterData">Addressable references for both combatants and the stage.</param>
        /// <param name="onProgress">Optional callback receiving a [0, 1] load progress fraction.</param>
        /// <returns>The fully loaded session ready for <see cref="ActivateSceneAsync"/>.</returns>
        public static async UniTask<CombatSession> LoadAsync(
            CombatEncounterData encounterData,
            Action<float> onProgress = null)
        {
            var session = new CombatSession();
            await session.LoadInternalAsync(encounterData, onProgress);
            return session;
        }

        /// <summary>
        /// Loads all data assets in parallel, then loads the stage scene and instantiates both
        /// combatant prefabs in parallel, reporting combined progress via <paramref name="onProgress"/>.
        /// </summary>
        /// <param name="data">Encounter data containing the Addressable references to load.</param>
        /// <param name="onProgress">Optional progress callback; receives values from 0 to 1.</param>
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

        /// <summary>Activates the loaded stage scene, making its GameObjects visible and active.</summary>
        public async UniTask ActivateSceneAsync()
        {
            await _stageHandle.Result.ActivateAsync().ToUniTask();
        }

        /// <summary>
        /// Releases all Addressable handles in order: combatant instances, stage scene,
        /// then data assets. Safe to call multiple times — invalid handles are skipped.
        /// </summary>
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

        /// <summary>Synchronous disposal shim; fires <see cref="DisposeAsync"/> as fire-and-forget.</summary>
        void IDisposable.Dispose() => DisposeAsync().Forget();
    }
}