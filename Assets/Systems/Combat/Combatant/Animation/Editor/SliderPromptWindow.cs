using System;
using UnityEditor;
using UnityEngine;

namespace Systems.Combat.Combatant.Animation.Editor
{
    public class SliderPromptWindow : EditorWindow
    {
        private int _initialValue;
        private int _minValue;
        private int _maxValue;
        private Action<int> _onConfirm;

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