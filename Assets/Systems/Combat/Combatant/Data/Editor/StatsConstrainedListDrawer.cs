#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Systems.Combat.Combatant.Data.Editor
{
    /// <summary>
    /// Custom property drawer for fields tagged with <see cref="StatsConstrainedListAttribute"/>.
    /// Renders a <see cref="ReorderableList"/> whose add-menu is filtered to
    /// <c>CombatantMove&lt;TStats&gt;</c> subclasses compatible with the sibling
    /// <c>StatsTemplate</c> field on the owning object.
    /// </summary>
    [CustomPropertyDrawer(typeof(StatsConstrainedListAttribute))]
    public sealed class StatsConstrainedListDrawer : PropertyDrawer
    {
        /// <summary>Per-property-path ReorderableList cache; drawers are shared across Inspector redraws.</summary>
        private readonly Dictionary<string, ReorderableList> _lists = new();

        // ── PropertyDrawer overrides ───────────────────────────────────────────────────

        /// <summary>Begins the property scope; the ReorderableList rendering is currently disabled pending refactor.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Debug.Log($"Drawing {property.serializedObject.targetObject.name}.{property.propertyPath}");

            EditorGUI.BeginProperty(position, label, property);
            // GetOrCreateList(property).DoList(position);
        }

        /// <summary>Returns single-line height for now; full list height is commented out pending refactor.</summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        // ── List construction ─────────────────────────────────────────────────────────

        /// <summary>Returns a cached <see cref="ReorderableList"/> for this property, creating and configuring it on first access.</summary>
        private ReorderableList GetOrCreateList(SerializedProperty property)
        {
            string key =
                $"{property.serializedObject.targetObject.GetInstanceID()}:{property.propertyPath}";
            if (_lists.TryGetValue(key, out var existing)) return existing;

            var list = new ReorderableList(
                property.serializedObject, property,
                draggable: true, displayHeader: true,
                displayAddButton: true, displayRemoveButton: true);

            // ── Header ─────────────────────────────────────────────────────────────
            list.drawHeaderCallback = rect =>
            {
                var statsType = CombatantMoveEditorUtils.GetStatsType(list.serializedProperty);
                string suffix = statsType != null
                    ? $"  —  {statsType.Name}"
                    : "  —  (no StatsTemplate)";
                EditorGUI.LabelField(rect, list.serializedProperty.displayName + suffix);
            };

            // ── Element height ──────────────────────────────────────────────────────
            // Fully delegated to CombatantMoveDrawer via GetPropertyHeight.
            // No extra height is added here — warnings / badges live inside the header row.
            list.elementHeightCallback = index =>
                EditorGUI.GetPropertyHeight(
                    list.serializedProperty.GetArrayElementAtIndex(index), includeChildren: true);

            // ── Element drawing ─────────────────────────────────────────────────────
            list.drawElementCallback = (rect, index, _, _) =>
            {
                // var element = list.serializedProperty.GetArrayElementAtIndex(index);
                // var statsType = CombatantMoveEditorUtils.GetStatsType(list.serializedProperty);
                //
                // // 1. Let CombatantMoveDrawer paint the full element (header + children).
                // EditorGUI.PropertyField(rect, element, GUIContent.none, includeChildren: true);
                //
                // // 2. Overlay the ▾ button in the margin CombatantMoveDrawer reserved.
                // //    IMGUI draws later calls on top, so the button appears above the bg rect.
                // var dropRect = new Rect(
                //     rect.xMax - CombatantMoveDrawer.ButtonMargin + 2,
                //     rect.y + 2,
                //     CombatantMoveDrawer.ButtonMargin - 4,
                //     CombatantMoveDrawer.HeaderHeight - 4);
                //
                // if (EditorGUI.DropdownButton(dropRect, new GUIContent("▾"),
                //         FocusType.Keyboard, EditorStyles.miniButton))
                // {
                //     // Capture stable references — property objects may be recycled.
                //     var currentType = element.managedReferenceValue?.GetType();
                //     var so = element.serializedObject;
                //     var path = element.propertyPath;
                //
                //     CombatantMoveEditorUtils.ShowTypeMenu(currentType, statsType, so, path);
                // }
            };

            // ── Add dropdown ────────────────────────────────────────────────────────
            list.onAddDropdownCallback = (buttonRect, _) =>
            {
                var prop = list.serializedProperty;
                var statsType = CombatantMoveEditorUtils.GetStatsType(prop);
                var so = prop.serializedObject;
                var arrayPath = prop.propertyPath;

                ShowAddMenu(buttonRect, statsType, so, arrayPath);
            };

            _lists[key] = list;
            return list;
        }

        // ── Add menu ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Shows a stats-constrained add menu. Lists all compatible move types or a disabled
        /// notice when no StatsTemplate is assigned or no matching types exist.
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

                                // arraySize++ is unreliable for [SerializeReference] arrays —
                                // Unity does not always dirty and resize the managed-reference
                                // backing store. InsertArrayElementAtIndex is the correct API.
                                int newIndex = prop.arraySize;
                                prop.InsertArrayElementAtIndex(newIndex);

                                // InsertArrayElementAtIndex copies the previous element, so
                                // always overwrite with a fresh instance.
                                var newProperty = prop.GetArrayElementAtIndex(newIndex);


                                newProperty.managedReferenceValue = Activator.CreateInstance(captured);

                                so.ApplyModifiedProperties();
                            });
                    }
                }
            }

            menu.DropDown(buttonRect);
        }
    }
}
#endif