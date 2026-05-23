using UnityEngine;

namespace Systems.UI.Transition
{
    [RequireComponent(typeof(CanvasGroup))]
    public class TransitionOverlay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        
        public CanvasGroup CanvasGroup => canvasGroup;
    }
}