using Reflex.Core;
using Reflex.Injectors;
using Systems.Combat.Camera;
using Systems.Core;
using Systems.UI.Combat;
using Systems.UI.CombatantSelect;
using Systems.UI.MainMenu;
using Systems.UI.Dev.CollisionVisualizer;
using Systems.UI.Dev.InputHistory.Scripts;
using Systems.UI.Transition;
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
            //   - MainMenu
            //     - UI_MainMenu (Canvas)
            //       - MainPanel (PlayButton, HelpButton, QuitButton)
            //       - HelpPanel (BackButton)
            //   - CharacterSelect
            //     - Canvas (Canvas)
            //     - Combat
            //      - CombatUICanvas (CombatUIController)
            //      - DebugInformation
            //          - InputHistoryVisualizer (UIDocument, InputHistoryUIList)
            //      - CombatCamera (CombatCamera, Camera)
            //   - Transition
            //     - TransitionCanvas (Canvas)
            //       - TransitionOverlay (TransitionOverlay, CanvasGroup)

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
            
            var mainMenuCanvas = instance.transform.Find("MainMenu")?.GetComponentInChildren<Canvas>(true);

            if (!mainMenuCanvas)
            {
                Debug.LogError(
                    "GameInitializer: Canvas component not found under 'MainMenu' in GlobalSystems prefab!");
                return;
            }

            var charSelectCanvas = instance.transform.Find("CharacterSelect")?.GetComponentInChildren<Canvas>(true);

            if (!charSelectCanvas)
            {
                Debug.LogError(
                    "GameInitializer: Canvas component not found under 'CharacterSelect' in GlobalSystems prefab!");
                return;
            }

            var combatUIController =
                instance.transform.Find("Combat/CombatUICanvas")?.GetComponent<CombatUIController>();

            var inputHistoryUIList = instance.transform.Find("Combat/DebugInformation/InputHistoryVisualizer")
                ?.GetComponent<InputHistoryUIList>();

            if (!inputHistoryUIList)
            {
                Debug.LogError(
                    "GameInitializer: InputHistoryUIList component not found at 'Combat/InputHistoryVisualizer' in GlobalSystems prefab!");
                return;
            }

            var combatCamera = instance.transform.Find("Combat/CombatCamera")?.GetComponent<CombatCamera>();

            if (!combatCamera)
            {
                Debug.LogError(
                    "GameInitializer: CombatCamera component not found at 'Combat/CombatCamera' in GlobalSystems prefab!");
                return;
            }

            var collisionVisualizer = instance.transform.Find("Combat/DebugInformation/CollisionVisualizer")
                ?.GetComponent<CollisionVisualizer>();

            var transitionOverlay = instance.transform.Find("Transition/TransitionCanvas/Overlay")
                ?.GetComponent<TransitionOverlay>();

            containerBuilder.RegisterValue(tickManager);
            containerBuilder.RegisterValue(new MainMenuCanvas(mainMenuCanvas));
            containerBuilder.RegisterValue(new CharacterSelectCanvas(charSelectCanvas));
            containerBuilder.RegisterValue(playerInputManager);
            containerBuilder.RegisterValue(combatUIController);

            containerBuilder.RegisterValue(inputHistoryUIList);
            containerBuilder.RegisterValue(collisionVisualizer);
            containerBuilder.RegisterValue(transitionOverlay);

            containerBuilder.OnContainerBuilt +=
                container =>
                    PostBuildInjection(tickManager, inputHistoryUIList, combatCamera, combatUIController, container);
        }

        private static void PostBuildInjection(TickManager tickManager, InputHistoryUIList inputHistoryUIList,
            CombatCamera combatCamera, CombatUIController combatUIController,
            Container container)
        {
            Debug.Log("GameInitializer: Performing post-build injection...");
            AttributeInjector.Inject(tickManager, container);
            AttributeInjector.Inject(inputHistoryUIList, container);
            AttributeInjector.Inject(combatCamera, container);
            AttributeInjector.Inject(combatUIController, container);
        }
    }
}