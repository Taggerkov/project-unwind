using Reflex.Core;
using Reflex.Injectors;
using Systems.Combat.Camera;
using Systems.Core;
using Systems.UI.Combat;
using Systems.UI.Overlay;
using Systems.UI.Menu.CombatantSelect;
using Systems.UI.Menu.MainMenu;
using Systems.UI.Dev.CollisionVisualizer;
using Systems.UI.Dev.InputHistory;
using Systems.UI.Core.Transition;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Systems
{
    /// <summary>
    /// Static entry point that wires the Reflex DI container before any scene loads. Instantiates
    /// the <c>GlobalSystems</c> prefab from Resources, resolves required components, registers them as
    /// Reflex values, and performs post-build attribute injection for MonoBehaviours that need
    /// injected references before their first tick.
    /// </summary>
    public static class GameInitializer
    {
        /// <summary>Registers <see cref="InjectGlobalSystems"/> as the Reflex root container builder delegate.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            ContainerScope.OnRootContainerBuilding += InjectGlobalSystems;
        }

        /// <summary>
        /// Loads and instantiates the GlobalSystems prefab, resolves all required singleton components,
        /// registers them in the Reflex container, and schedules post-build attribute injection.
        /// Logs an error and returns early when any required component is missing.
        /// </summary>
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

            // Expected GlobalSystems prefab hierarchy:
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
            containerBuilder.RegisterValue(new CombatantSelectCanvas(charSelectCanvas));
            containerBuilder.RegisterValue(playerInputManager);
            containerBuilder.RegisterValue(combatUIController);

            containerBuilder.RegisterValue(inputHistoryUIList);
            containerBuilder.RegisterValue(collisionVisualizer);
            containerBuilder.RegisterValue(transitionOverlay);

            containerBuilder.OnContainerBuilt +=
                container =>
                    PostBuildInjection(tickManager, inputHistoryUIList, combatCamera, combatUIController, container);
        }

        /// <summary>
        /// Performs Reflex attribute injection on MonoBehaviours that cannot receive constructor injection
        /// because they are instantiated by Unity rather than the container.
        /// </summary>
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