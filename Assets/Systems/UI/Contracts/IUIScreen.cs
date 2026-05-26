using Systems.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.UI.Contracts
{
    /// <summary>
    /// A self-contained menu screen (e.g. main menu, character select). Owns its panels, button
    /// routing and domain events, but delegates all controller, cursor and action-map handling to
    /// <see cref="UIManager"/>. The manager shows exactly one screen at a time and feeds it navigation
    /// and submit notifications for the controllers it has attached.
    /// </summary>
    public interface IUIScreen
    {
        /// <summary>The cursor model this screen uses.</summary>
        CursorMode CursorMode { get; }

        /// <summary>
        /// The transform the shared cursor prefab is instantiated under (the screen's canvas). The
        /// manager owns the cursor objects; the screen only says where they live.
        /// </summary>
        Transform CursorParent { get; }

        /// <summary>
        /// Shows the screen and sets its initial state. The context is retained for the screen's
        /// lifetime so it can request selection and cursor changes.
        /// </summary>
        /// <param name="context">The manager surface used to drive selection and cursors.</param>
        void Enter(IUIContext context);

        /// <summary>Hides the screen and clears any transient state.</summary>
        void Exit();

        /// <summary>
        /// The selectable a controller should focus when it attaches and no prior selection is held;
        /// also the target restored on reconnect.
        /// </summary>
        /// <param name="playerId">The player the focus is for.</param>
        /// <returns>The default selectable, or null if the screen has none.</returns>
        Selectable GetDefaultSelectable(int playerId);

        /// <summary>Called after the manager attaches a controller, for screen-specific setup.</summary>
        /// <param name="linker">The controller that was attached.</param>
        void OnPlayerAttached(PlayerLinker linker);

        /// <summary>Called after the manager detaches a controller, for screen-specific cleanup.</summary>
        /// <param name="linker">The controller that was detached.</param>
        void OnPlayerDetached(PlayerLinker linker);

        /// <summary>Called when an attached controller moves its selection.</summary>
        /// <param name="linker">The controller that navigated.</param>
        /// <param name="previous">The previously focused selectable, or null.</param>
        /// <param name="current">The newly focused selectable.</param>
        void OnNavigate(PlayerLinker linker, Selectable previous, Selectable current);

        /// <summary>Called when an attached controller submits on its current selectable.</summary>
        /// <param name="linker">The controller that submitted.</param>
        /// <param name="selectable">The selectable that was focused when submit fired.</param>
        void OnSubmit(PlayerLinker linker, Selectable selectable);
    }
}
