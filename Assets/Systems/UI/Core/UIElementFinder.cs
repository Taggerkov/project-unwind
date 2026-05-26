using UnityEngine;

namespace Systems.UI.Core
{
    /// <summary>
    /// Helpers for resolving named child objects and components inside a UI canvas hierarchy.
    /// Used by screen constructors to find their required elements by name without duplicating
    /// the find-and-null-check pattern across every screen.
    /// </summary>
    internal static class UIElementFinder
    {
        /// <summary>Finds a named child of <paramref name="parent"/> and returns its <see cref="GameObject"/>.</summary>
        /// <param name="parent">The transform to search under.</param>
        /// <param name="name">The child name to find.</param>
        /// <param name="result">The found game object, or null if absent.</param>
        /// <returns>True when a matching child exists.</returns>
        internal static bool TryFind(Transform parent, string name, out GameObject result)
        {
            result = parent.Find(name)?.gameObject;
            return result;
        }

        /// <summary>Finds a named child of <paramref name="parent"/> and returns its <typeparamref name="T"/> component.</summary>
        /// <typeparam name="T">The component type to retrieve.</typeparam>
        /// <param name="parent">The transform to search under.</param>
        /// <param name="name">The child name to find.</param>
        /// <param name="result">The found component, or null if the child or component is absent.</param>
        /// <returns>True when a matching child with the component exists.</returns>
        internal static bool TryFind<T>(Transform parent, string name, out T result) where T : Component
        {
            result = parent.Find(name)?.GetComponent<T>();
            return result;
        }
    }
}
