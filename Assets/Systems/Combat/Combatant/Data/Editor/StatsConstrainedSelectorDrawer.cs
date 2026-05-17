#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Systems.Combat.Combatant.Data.Editor
{
    [CustomPropertyDrawer(typeof(StatsConstrainedSelector))]
    public sealed class StatsConstrainedSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(
            Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.HelpBox(position,
                    $"[StatsConstrainedSelector] requires [SerializeReference] on '{property.name}'.",
                    MessageType.Error);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var statsType = CombatantMoveEditorUtils.GetStatsType(property);
            var currentType = property.managedReferenceValue?.GetType();
            var buttonLabel = currentType != null
                ? CombatantMoveEditorUtils.NiceName(currentType)
                : "<None>";

            // Mismatch tint — same colour convention as CombatantMoveDrawer's planned warning
            bool mismatch = currentType != null
                            && CombatantMoveEditorUtils.GetGenericStatsArgument(currentType) is { } arg
                            && statsType != null
                            && arg != statsType;

            if (mismatch)
                buttonLabel = "⚠  " + buttonLabel;

            var labelRect = new Rect(
                position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            var btnRect = new Rect(
                position.x + EditorGUIUtility.labelWidth, position.y,
                position.width - EditorGUIUtility.labelWidth, position.height);

            EditorGUI.LabelField(labelRect, label);

            // Capture stable references before the menu opens
            var so = property.serializedObject;
            var path = property.propertyPath;

            if (EditorGUI.DropdownButton(btnRect, new GUIContent(buttonLabel), FocusType.Keyboard))
                CombatantMoveEditorUtils.ShowTypeMenu(currentType, statsType, so, path);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;
    }
}
#endif