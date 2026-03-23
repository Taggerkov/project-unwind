using System;
using Reflex.Core;
using Reflex.Enums;
using Reflex.Injectors;
using Systems.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Resolution = Reflex.Enums.Resolution;

namespace Systems
{
    public static class GameInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            ContainerScope.OnRootContainerBuilding += InjectGlobalSystems;
        }

        private static void InjectGlobalSystems(ContainerBuilder containerBuilder)
        {
            Debug.Log("GameInitializer: Injecting global systems...");
            GameObject prefab = Resources.Load<GameObject>("GlobalSystems");

            if (prefab == null)
            {
                Debug.LogError("GameInitializer: Could not find 'GlobalSystems' prefab in Resources!");
                return;
            }

            GameObject instance = Object.Instantiate(prefab);
            instance.name = "[Global Systems]";

            Object.DontDestroyOnLoad(instance);

            //Expected GlobalSystems structure:
            // - GlobalSystems (TickManager, PlayerInputManager, KinematicCharacterSystem)
            //   - CharacterSelect (UIDocument)

            var tickManager = instance.GetComponent<TickManager>();
            if (!tickManager)
            {
                Debug.LogError("GameInitializer: TickManager component not found on GlobalSystems prefab!");
                return;
            }

            var playerInputManager = instance.GetComponent<PlayerInputManager>();

            if (!playerInputManager)
            {
                Debug.LogError("GameInitializer: PlayerInputManager component not found on GlobalSystems prefab!");
                return;
            }

            var document = instance.transform.Find("CharacterSelect")?.GetComponent<UIDocument>();

            if (!document)
            {
                Debug.LogError(
                    "GameInitializer: UIDocument component not found on CharacterSelect child of GlobalSystems prefab!");
                return;
            }

            containerBuilder.RegisterValue(playerInputManager);
            containerBuilder.RegisterValue(document);

            containerBuilder.RegisterType(typeof(PlayerRegistry), new[] { typeof(PlayerRegistry), typeof(IDisposable) },
                Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(CharacterSelectManager),
                new[] { typeof(CharacterSelectManager), typeof(IDisposable) },
                Lifetime.Singleton, Resolution.Eager);

            containerBuilder.OnContainerBuilt += (container) => PostBuildInjection(tickManager, container);
        }

        private static void PostBuildInjection(TickManager tickManager, Container container)
        {
            Debug.Log("GameInitializer: Performing post-build injection...");
            AttributeInjector.Inject(tickManager, container);
        }
    }
}