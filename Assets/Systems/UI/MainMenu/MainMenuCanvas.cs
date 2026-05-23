using Systems.Common;
using UnityEngine;

namespace Systems.UI.MainMenu
{
    /// <summary>
    /// Strongly typed dependency-injection wrapper around the main menu's root <see cref="Canvas"/>,
    /// so the menu canvas can be registered and injected without colliding with other canvases.
    /// </summary>
    public class MainMenuCanvas : TypedWrapper<Canvas>
    {
        /// <summary>Wraps the supplied main menu root <see cref="Canvas"/>.</summary>
        /// <param name="value">The main menu's root canvas instance.</param>
        public MainMenuCanvas(Canvas value) : base(value)
        {
        }
    }
}
