using System;
using UnityEditor;
using UnityEngine;

namespace Systems.Combat.Combatant.Animation.Editor
{
    /// <summary>
    /// Modal utility window that shows an integer slider and a Confirm button. Used by the
    /// Scriptable Animation Editor to let the user choose a pose offset before committing a capture.
    /// </summary>
    public class SliderPromptWindow : EditorWindow
    {
        /// <summary>Current slider value; initialised from <see cref="ShowWindow"/> and updated by the user.</summary>
        private int _initialValue;

        /// <summary>Minimum selectable slider value.</summary>
        private int _minValue;

        /// <summary>Maximum selectable slider value.</summary>
        private int _maxValue;

        /// <summary>Callback invoked with the confirmed integer when the user clicks Confirm.</summary>
        private Action<int> _onConfirm;

        /// <summary>Opens the prompt window with the given range and initial value; invokes <paramref name="onConfirm"/> on confirmation.</summary>
        public static void ShowWindow(int initialValue, int minValue, int maxValue, Action<int> onConfirm)
        {
            var window = GetWindow<SliderPromptWindow>(true, "Select Offset", true);
            window.minSize = new Vector2(800, 120);
            window.maxSize = new Vector2(800, 120);
            window._initialValue = initialValue;
            window._minValue = minValue;
            window._maxValue = maxValue;
            window._onConfirm = onConfirm;
            window.ShowUtility();
        }

        /// <summary>Renders the slider and Confirm button; closes the window and fires <see cref="_onConfirm"/> on confirmation.</summary>
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            _initialValue = EditorGUILayout.IntSlider("Pose Offset", _initialValue, _minValue, _maxValue);
            EditorGUILayout.Space(10);

            if (GUILayout.Button("Confirm", GUILayout.Height(28)))
            {
                _onConfirm?.Invoke(_initialValue);
                Close();
            }
        }
    }
}