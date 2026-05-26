using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Systems.UI.Menu.MainMenu
{
    /// <summary>
    /// Scrolls a <see cref="ScrollRect"/> from the scroll actions of one or more controllers, supporting
    /// a fixed nudge on tap and a continuous, frame-rate-independent scroll while held. Any controller's
    /// scroll buttons drive the same content. Owns the borrowed actions and the polling loop for the
    /// lifetime of a <see cref="Begin"/>/<see cref="Stop"/> pair.
    /// </summary>
    public class HelpScrollController
    {
        /// <summary>The scroll view this controller drives.</summary>
        private readonly ScrollRect _scroll;

        /// <summary>Name of the action that scrolls toward the top.</summary>
        private readonly string _scrollUpActionName;

        /// <summary>Name of the action that scrolls toward the bottom.</summary>
        private readonly string _scrollDownActionName;

        /// <summary>Resolved "scroll up" actions, one per controller, enabled only between Begin and Stop.</summary>
        private readonly List<InputAction> _upActions = new();

        /// <summary>Resolved "scroll down" actions, one per controller, enabled only between Begin and Stop.</summary>
        private readonly List<InputAction> _downActions = new();

        /// <summary>Cancels the polling loop when scrolling stops.</summary>
        private CancellationTokenSource _cts;

        /// <summary>Pixels scrolled by a single tap.</summary>
        private const float TapPixels = 120f;

        /// <summary>Seconds a button must be held before continuous scrolling begins.</summary>
        private const float RepeatDelay = 0.35f;

        /// <summary>Continuous scroll speed once the hold delay has elapsed.</summary>
        private const float RepeatPixelsPerSecond = 1200f;

        /// <summary>Binds the controller to a scroll view and the names of the actions that drive it.</summary>
        /// <param name="scroll">The scroll view to drive.</param>
        /// <param name="scrollUpActionName">Action name that scrolls toward the top.</param>
        /// <param name="scrollDownActionName">Action name that scrolls toward the bottom.</param>
        public HelpScrollController(ScrollRect scroll, string scrollUpActionName, string scrollDownActionName)
        {
            _scroll = scroll;
            _scrollUpActionName = scrollUpActionName;
            _scrollDownActionName = scrollDownActionName;
        }

        /// <summary>
        /// Resolves and enables the scroll actions on every supplied controller and starts the polling
        /// loop. Restarts cleanly if already running. No-op without a scroll view or any controller.
        /// </summary>
        /// <param name="playerInputs">The controllers whose actions drive scrolling.</param>
        public void Begin(IReadOnlyList<PlayerInput> playerInputs)
        {
            Stop();
            if (!_scroll || playerInputs == null) return;

            foreach (var playerInput in playerInputs)
            {
                if (playerInput == null) continue;

                var up = playerInput.actions.FindAction(_scrollUpActionName);
                if (up != null)
                {
                    up.Enable();
                    _upActions.Add(up);
                }

                var down = playerInput.actions.FindAction(_scrollDownActionName);
                if (down != null)
                {
                    down.Enable();
                    _downActions.Add(down);
                }
            }

            if (_upActions.Count == 0 && _downActions.Count == 0) return;

            _cts = new CancellationTokenSource();
            Loop(_cts.Token).Forget();
        }

        /// <summary>Stops the polling loop, disables every borrowed action, and clears their caches.</summary>
        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            foreach (var action in _upActions) action?.Disable();
            foreach (var action in _downActions) action?.Disable();
            _upActions.Clear();
            _downActions.Clear();
        }

        /// <summary>Snaps the scroll view to the top. No-op without a scroll view.</summary>
        public void ResetToTop()
        {
            if (_scroll) _scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// Polls both scroll directions each frame until cancelled, applying a tap step on press and a
        /// continuous scroll once a button is held past <see cref="RepeatDelay"/>.
        /// </summary>
        /// <param name="token">Cancels the loop when scrolling stops.</param>
        private async UniTaskVoid Loop(CancellationToken token)
        {
            var upHeldSeconds = -1f;
            var downHeldSeconds = -1f;

            while (!token.IsCancellationRequested)
            {
                var deltaTime = Time.unscaledDeltaTime;
                StepAxis(AnyPressed(_upActions), 1f, ref upHeldSeconds, deltaTime);
                StepAxis(AnyPressed(_downActions), -1f, ref downHeldSeconds, deltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            }
        }

        /// <summary>Returns true when any action in the list is currently pressed.</summary>
        /// <param name="actions">The actions to test.</param>
        /// <returns>True if at least one action is pressed.</returns>
        private static bool AnyPressed(List<InputAction> actions)
        {
            foreach (var action in actions)
                if (action != null && action.IsPressed())
                    return true;
            return false;
        }

        /// <summary>
        /// Advances one scroll direction for a frame: nudges by a fixed amount on the press edge, then
        /// scrolls continuously once held past <see cref="RepeatDelay"/>; resets when released.
        /// </summary>
        /// <param name="pressed">Whether the direction is held this frame by any controller.</param>
        /// <param name="direction">Scroll sign: positive scrolls up, negative scrolls down.</param>
        /// <param name="heldSeconds">Per-direction hold timer; negative while released.</param>
        /// <param name="deltaTime">Unscaled time elapsed since the previous poll.</param>
        private void StepAxis(bool pressed, float direction, ref float heldSeconds, float deltaTime)
        {
            if (!pressed)
            {
                heldSeconds = -1f;
                return;
            }

            if (heldSeconds < 0f)
            {
                Scroll(direction * TapPixels);
                heldSeconds = 0f;
                return;
            }

            heldSeconds += deltaTime;
            if (heldSeconds >= RepeatDelay)
                Scroll(direction * RepeatPixelsPerSecond * deltaTime);
        }

        /// <summary>
        /// Scrolls the content by a pixel amount, converting to the scroll rect's normalised range and
        /// clamping to its bounds. No-op when the content fits within the viewport.
        /// </summary>
        /// <param name="pixels">Pixels to scroll; positive moves toward the top, negative toward the bottom.</param>
        private void Scroll(float pixels)
        {
            if (!_scroll || !_scroll.content || !_scroll.viewport) return;

            var scrollable = _scroll.content.rect.height - _scroll.viewport.rect.height;
            if (scrollable <= 0f) return;

            var deltaNormalized = pixels / scrollable;
            var target = _scroll.verticalNormalizedPosition + deltaNormalized;
            _scroll.verticalNormalizedPosition = Mathf.Clamp01(target);
        }
    }
}
