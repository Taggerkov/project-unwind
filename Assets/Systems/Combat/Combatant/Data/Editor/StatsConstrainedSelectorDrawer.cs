#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Systems.Combat.Combatant.Data.Editor
{
    /// <summary>
    /// Custom property drawer for fields tagged with <see cref="StatsConstrainedSelector"/>.
    /// Renders a single dropdown line that opens a type-filtered menu compatible with the
    /// sibling <c>StatsTemplate</c> field. Highlights mismatched assignments with a ⚠ prefix.
    /// </summary>
    [CustomPropertyDrawer(typeof(StatsConstrainedSelector))]
    public sealed class StatsConstrainedSelectorDrawer : PropertyDrawer
    {
        /// <summary>Draws the label and a dropdown button; shows an error box when the target field is not a <c>[SerializeReference]</c>.</summary>
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

        /// <summary>Always returns single-line height; this drawer never expands children inline.</summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;
    }
}
#endif