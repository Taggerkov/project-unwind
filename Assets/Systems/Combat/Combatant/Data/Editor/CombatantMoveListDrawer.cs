using System;
using System.Linq;
using Systems.Combat.Combatant.Behaviour;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Systems.Combat.Combatant.Data.Editor
{
    /// <summary>
    /// Custom property drawer for <see cref="CombatantMoveList"/>. Renders the inner
    /// <c>List&lt;CombatantMove&gt;</c> as a <see cref="ReorderableList"/> with a type-filtered
    /// add menu and a per-element ▾ dropdown for changing the concrete move type.
    /// </summary>
    [CustomPropertyDrawer(typeof(CombatantMoveList))]
    public class CombatantMoveListDrawer : PropertyDrawer
    {
        /// <summary>Pixels reserved on the right of each element for the parent's ▾ type-selector button overlay.</summary>
        internal const float ButtonMargin = 26f;

        /// <summary>Pixels reserved on the left of each element for the ReorderableList drag handle.</summary>
        internal const float DraggableMargin = 8f;

        /// <summary>Per-property ReorderableList instances keyed by a stable object/path string.</summary>
        private readonly System.Collections.Generic.Dictionary<string, ReorderableList> _lists = new();

        /// <summary>Delegates rendering to the cached <see cref="ReorderableList"/> for this property.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            GetOrCreateList(property).DoList(position);
            EditorGUI.EndProperty();
        }

        /// <summary>Returns the height reported by the cached <see cref="ReorderableList"/>.</summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return GetOrCreateList(property).GetHeight();
        }

        /// <summary>
        /// Returns the <see cref="ReorderableList"/> for this property, creating and configuring
        /// it on first access. The list is keyed by instance ID and property path to survive
        /// Inspector redraws without losing scroll position or selection state.
        /// </summary>
        private ReorderableList GetOrCreateList(SerializedProperty property)
        {
            string key = $"{property.serializedObject.targetObject.GetInstanceID()}:{property.propertyPath}";

            if (!_lists.TryGetValue(key, out ReorderableList list))
            {
                // Drill into the wrapper to get the actual List<CombatantMove> field
                SerializedProperty innerList = property.FindPropertyRelative("list");

                list = new ReorderableList(
                    property.serializedObject,
                    innerList,
                    draggable: true,
                    displayHeader: true,
                    displayAddButton: true,
                    displayRemoveButton: true
                );

                list.drawHeaderCallback = rect =>
                {
                    var statsType = CombatantMoveEditorUtils.GetStatsType(list.serializedProperty);
                    string suffix = statsType != null
                        ? $"  —  {statsType.Name}"
                        : "  —  (no StatsTemplate)";

                    EditorGUI.LabelField(rect, list.serializedProperty.displayName + suffix);
                };

                list.drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var element = list.serializedProperty.GetArrayElementAtIndex(index);
                    var statsType = CombatantMoveEditorUtils.GetStatsType(list.serializedProperty);

                    var modRect = new Rect(rect.x + DraggableMargin, rect.y, rect.width - DraggableMargin - ButtonMargin, rect.height);

                    // 1. Let CombatantMoveDrawer paint the full element (header + children).
                    EditorGUI.PropertyField(modRect, element, GUIContent.none, includeChildren: true);

                    // 2. Overlay the ▾ button in the margin CombatantMoveDrawer reserved.
                    //    IMGUI draws later calls on top, so the button appears above the bg rect.
                    var dropRect = new Rect(
                        rect.xMax - ButtonMargin + 2,
                        rect.y + 2,
                        ButtonMargin - 4,
                        rect.height - 4);

                    if (EditorGUI.DropdownButton(dropRect, new GUIContent("▾"),
                            FocusType.Keyboard, EditorStyles.miniButton))
                    {
                        // Capture stable references — property objects may be recycled.
                        var currentType = element.managedReferenceValue?.GetType();
                        var so = element.serializedObject;
                        var path = element.propertyPath;

                        CombatantMoveEditorUtils.ShowTypeMenu(currentType, statsType, so, path);
                    }
                };

                list.elementHeightCallback = index =>
                {
                    SerializedProperty element = innerList.GetArrayElementAtIndex(index);
                    return EditorGUI.GetPropertyHeight(element, true) + 4f;
                };
                

                list.onAddDropdownCallback = (buttonRect, l) =>
                {
                    var prop = list.serializedProperty;
                    var statsType = CombatantMoveEditorUtils.GetStatsType(prop);
                    var so = prop.serializedObject;
                    var arrayPath = prop.propertyPath;

                    ShowAddMenu(buttonRect, statsType, so, arrayPath);
                };

                list.onRemoveCallback = l =>
                {
                    ReorderableList.defaultBehaviours.DoRemoveButton(l);
                    property.serializedObject.ApplyModifiedProperties();
                };

                _lists[key] = list;
            }

            return list;
        }

        /// <summary>
        /// Builds and shows a <see cref="GenericMenu"/> listing all <c>CombatantMove&lt;TStats&gt;</c>
        /// types compatible with <paramref name="statsType"/>, grouped by namespace path.
        /// Selecting a type appends a new instance to the serialized array.
        /// </summary>
        private static void ShowAddMenu(
            Rect buttonRect, Type statsType, SerializedObject so, string arrayPath)
        {
            var menu = new GenericMenu();

            if (statsType == null)
            {
                menu.AddDisabledItem(new GUIContent("Assign a StatsTemplate first"));
            }
            else
            {
                var types = CombatantMoveEditorUtils
                    .GetCompatibleMoveTypes(statsType)
                    .OrderBy(t => t.FullName)
                    .ToList();

                if (types.Count == 0)
                {
                    menu.AddDisabledItem(
                        new GUIContent($"No CombatantMove<{statsType.Name}> types found in project"));
                }
                else
                {
                    foreach (var type in types)
                    {
                        var captured = type;
                        menu.AddItem(
                            new GUIContent(CombatantMoveEditorUtils.GetMenuPath(type)),
                            false,
                            () =>
                            {
                                var prop = so.FindProperty(arrayPath);
                                if (prop == null) return;

                                so.Update();

                                int newIndex = prop.arraySize;
                                prop.InsertArrayElementAtIndex(newIndex);
                                prop.GetArrayElementAtIndex(newIndex).managedReferenceValue =
                                    Activator.CreateInstance(captured);
                                so.ApplyModifiedProperties();
                            });
                    }
                }
            }

            menu.DropDown(buttonRect);
            menu.ShowAsContext();
        }
    }
}