using Systems.Common;
using UnityEngine;

namespace Systems.UI.Menu.CombatantSelect
{
    /// <summary>
    /// Strongly typed dependency-injection wrapper around the character select root <see cref="Canvas"/>,
    /// so it can be registered and injected without colliding with other canvases.
    /// </summary>
    public class CombatantSelectCanvas : TypedWrapper<Canvas>
    {
        /// <summary>Wraps the supplied character select root <see cref="Canvas"/>.</summary>
        /// <param name="value">The character select root canvas instance.</param>
        public CombatantSelectCanvas(Canvas value) : base(value)
        {
        }
    }
}