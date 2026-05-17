using Reflex.Core;
using Reflex.Injectors;
using Systems.Core;
using Systems.UI.CombatantSelect;
using Systems.UI.Dev.CollisionVisualizer;
using Systems.UI.Dev.InputHistory.Scripts;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

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
            //   - CharacterSelect
            //     - Canvas (Canvas)
            //     - Combat
            //      - DebugInformation
            //          - InputHistoryVisualizer (UIDocument, InputHistoryUIList)

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

            var canvas = instance.transform.Find("CharacterSelect/Canvas")?.GetComponent<Canvas>();

            if (!canvas)
            {
                Debug.LogError(
                    "GameInitializer: Canvas component not found at 'CharacterSelect/Canvas' in GlobalSystems prefab!");
                return;
            }

            var inputHistoryUIList = instance.transform.Find("Combat/DebugInformation/InputHistoryVisualizer")
                ?.GetComponent<InputHistoryUIList>();

            if (!inputHistoryUIList)
            {
                Debug.LogError(
                    "GameInitializer: InputHistoryUIList component not found at 'Combat/InputHistoryVisualizer' in GlobalSystems prefab!");
                return;
            }

            var collisionVisualizer = instance.transform.Find("Combat/DebugInformation/CollisionVisualizer")
                ?.GetComponent<CollisionVisualizer>();

            containerBuilder.RegisterValue(tickManager);
            containerBuilder.RegisterValue(new CharacterSelectCanvas(canvas));
            containerBuilder.RegisterValue(playerInputManager);

            containerBuilder.RegisterValue(inputHistoryUIList);
            containerBuilder.RegisterValue(collisionVisualizer);

            containerBuilder.OnContainerBuilt +=
                container => PostBuildInjection(tickManager, inputHistoryUIList, container);
        }

        private static void PostBuildInjection(TickManager tickManager, InputHistoryUIList inputHistoryUIList,
            Container container)
        {
            Debug.Log("GameInitializer: Performing post-build injection...");
            AttributeInjector.Inject(tickManager, container);
            AttributeInjector.Inject(inputHistoryUIList, container);
        }
    }
}