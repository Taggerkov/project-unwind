using System;
using System.Linq;
using Systems.Combat.Combatant.Behaviour;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Systems.Combat.Combatant.Data.Editor
{
    [CustomPropertyDrawer(typeof(CombatantMoveList))]
    public class CombatantMoveListDrawer : PropertyDrawer
    {
        internal const float ButtonMargin = 26f; // reserved for parent's ▾ button overlay
        internal const float DraggableMargin = 8f; // reserved for ReorderableList's drag handle

        private readonly System.Collections.Generic.Dictionary<string, ReorderableList> _lists = new();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            GetOrCreateList(property).DoList(position);
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return GetOrCreateList(property).GetHeight();
        }

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