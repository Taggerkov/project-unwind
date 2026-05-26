using UnityEngine;

namespace Systems.UI.Core.Transition
{
    /// <summary>
    /// MonoBehaviour anchor for the full-screen fade overlay. Exposes the <see cref="CanvasGroup"/>
    /// that <see cref="TransitionManager"/> animates during screen changes.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class TransitionOverlay : MonoBehaviour
    {
        /// <summary>The canvas group whose alpha is animated during fade transitions.</summary>
        [SerializeField] private CanvasGroup canvasGroup;

        /// <summary>The canvas group whose alpha is animated during fade transitions.</summary>
        public CanvasGroup CanvasGroup => canvasGroup;
    }
}