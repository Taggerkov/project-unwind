using System.Collections.Generic;
using Systems.Input;
using UnityEngine.UI;

namespace Systems.UI.Contracts
{
    /// <summary>
    /// The narrow surface a screen uses to ask <see cref="UIManager"/> to move selection or cursors.
    /// Screens never touch event systems, action maps or controller lifecycle directly; they express
    /// intent through this context and the manager performs the infrastructure work.
    /// </summary>
    public interface IUIContext
    {
        /// <summary>Controllers currently attached to the active screen, in join order.</summary>
        IReadOnlyList<PlayerLinker> ActiveLinkers { get; }

        /// <summary>
        /// Moves the single shared cursor to <paramref name="selectable"/>, mirroring it onto every
        /// attached controller. Valid only for <see cref="CursorMode.Shared"/> screens.
        /// </summary>
        /// <param name="selectable">The selectable to focus on every controller.</param>
        void SetSharedSelection(Selectable selectable);

        /// <summary>
        /// Moves one player's cursor and event-system focus to <paramref name="selectable"/>. Valid
        /// only for <see cref="CursorMode.PerPlayer"/> screens.
        /// </summary>
        /// <param name="playerId">The player whose cursor to move.</param>
        /// <param name="selectable">The selectable to focus for that player.</param>
        void SetSelection(int playerId, Selectable selectable);

        /// <summary>
        /// Enables or disables a player's UI input and cursor. Disabling reverts the controller to the
        /// gameplay action map and hides its cursor (e.g. once it has locked in a choice). Valid only
        /// for <see cref="CursorMode.PerPlayer"/> screens.
        /// </summary>
        /// <param name="playerId">The player to enable or disable.</param>
        /// <param name="enabled">True to enable UI input and show the cursor; false to disable both.</param>
        void SetPlayerEnabled(int playerId, bool enabled);

        /// <summary>Recomputes cursor visibility and placement from the current selections.</summary>
        void RefreshCursors();
    }
}
