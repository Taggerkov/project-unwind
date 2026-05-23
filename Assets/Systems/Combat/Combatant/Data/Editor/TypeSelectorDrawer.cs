using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Systems.Combat.Combatant.Data.Editor
{
    [CustomPropertyDrawer(typeof(TypeSelector))]
    public class TypeSelectorDrawer : PropertyDrawer
    {
        // ── Subclass cache (per base type, built once) ─────────────────────────────────
        private static readonly Dictionary<Type, List<Type>> SubclassCache = new();

        // ── Custom-drawer cache (per concrete type, built once) ────────────────────────
        // null sentinel means "no custom drawer found for this type".
        private static readonly Dictionary<Type, PropertyDrawer> TypeDrawerCache = new();

        // Reflection handles into CustomPropertyDrawer's private backing fields,
        // used to read which type a drawer targets and whether it covers children.
        private static readonly FieldInfo s_CpdType =
            typeof(CustomPropertyDrawer).GetField("m_Type", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo s_CpdUseForChildren =
            typeof(CustomPropertyDrawer).GetField("m_UseForChildren", BindingFlags.Instance | BindingFlags.NonPublic);

        // ── Layout constants ───────────────────────────────────────────────────────────
        private const float DropdownHeight = 20f;
        private const float Spacing = 2f;

        // ── GUI ────────────────────────────────────────────────────────────────────────

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.HelpBox(position,
                    $"[SubclassSelector] requires [SerializeReference] on field '{property.name}'.",
                    MessageType.Error);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            // ── Dropdown ──────────────────────────────────────────────────────────────
            var dropdownRect = new Rect(position.x, position.y, position.width, DropdownHeight);
            DrawTypeDropdown(dropdownRect, property, label);

            // ── Child fields ──────────────────────────────────────────────────────────
            if (property.managedReferenceValue != null && property.hasVisibleChildren &&
                ((TypeSelector)attribute).DrawChildren)
            {
                float y = position.y + DropdownHeight + Spacing;
                DrawChildren(position, property, ref y);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
                return EditorGUIUtility.singleLineHeight;

            float height = DropdownHeight;

            if (property.managedReferenceValue != null && property.hasVisibleChildren &&
                ((TypeSelector)attribute).DrawChildren)
            {
                height += ChildrenHeight(property);
            }

            return height;
        }

        // ── Dropdown drawing ───────────────────────────────────────────────────────────

        private void DrawTypeDropdown(Rect rect, SerializedProperty property, GUIContent label)
        {
            var baseType = GetManagedReferenceBaseType(property);
            var currentType = property.managedReferenceValue?.GetType();
            var buttonLabel = currentType != null
                ? NiceName(currentType)
                : $"<None>  ({NiceName(baseType)})";

            float labelWidth = EditorGUIUtility.labelWidth;
            var labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            var btnRect = new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, rect.height);

            EditorGUI.LabelField(labelRect, label);

            if (EditorGUI.DropdownButton(btnRect, new GUIContent(buttonLabel), FocusType.Keyboard))
                ShowTypeMenu(property, baseType, currentType);
        }

        private static void ShowTypeMenu(SerializedProperty property, Type baseType, Type currentType)
        {
            var menu = new GenericMenu();
            var subTypes = GetSubclasses(baseType);

            menu.AddItem(new GUIContent("None"), currentType == null, () => SetType(property, null));
            menu.AddSeparator("");

            foreach (var type in subTypes)
            {
                var t = type;
                menu.AddItem(new GUIContent(GetMenuPath(type)), currentType == type, () => SetType(property, t));
            }

            menu.ShowAsContext();
        }

        private static void SetType(SerializedProperty property, Type type)
        {
            property.managedReferenceValue = type != null ? Activator.CreateInstance(type) : null;
            property.serializedObject.ApplyModifiedProperties();
        }

        // ── Child property drawing ─────────────────────────────────────────────────────

        /// <summary>
        /// Draws the children of <paramref name="property"/>.  If a <see cref="PropertyDrawer"/>
        /// is registered for the concrete managed-reference type (e.g. <c>CombatantStatsDrawer</c>),
        /// that drawer is invoked on the whole property so it can lay out its own grouping.
        /// Otherwise we fall back to drawing each visible child individually.
        /// </summary>
        private static void DrawChildren(Rect position, SerializedProperty property, ref float y)
        {
            var customDrawer = FindTypeDrawer(property.managedReferenceValue?.GetType());

            if (customDrawer != null)
            {
                float h = customDrawer.GetPropertyHeight(property, GUIContent.none);
                var rect = new Rect(position.x, y, position.width, h);
                customDrawer.OnGUI(rect, property, GUIContent.none);
                y += h;
                return;
            }

            // ── Fallback: draw each serialized child individually ──────────────────────
            EditorGUI.indentLevel++;

            foreach (var child in IterateVisibleChildren(property))
            {
                float childHeight = EditorGUI.GetPropertyHeight(child, true);
                var childRect = new Rect(position.x, y, position.width, childHeight);
                EditorGUI.PropertyField(childRect, child, true);
                y += childHeight + Spacing;
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// Returns the total height needed for the children area, delegating to a
        /// registered <see cref="PropertyDrawer"/> when available.
        /// </summary>
        private static float ChildrenHeight(SerializedProperty property)
        {
            var customDrawer = FindTypeDrawer(property.managedReferenceValue?.GetType());

            if (customDrawer != null)
                return customDrawer.GetPropertyHeight(property, GUIContent.none);

            // Fallback
            float h = 0f;
            foreach (var child in IterateVisibleChildren(property))
                h += EditorGUI.GetPropertyHeight(child, true) + Spacing;
            return h;
        }

        // ── Type-drawer lookup ─────────────────────────────────────────────────────────

        /// <summary>
        /// Finds and caches a <see cref="PropertyDrawer"/> whose <c>[CustomPropertyDrawer]</c>
        /// targets <paramref name="concreteType"/> or one of its base types
        /// (when <c>useForChildren: true</c>).
        /// Returns <c>null</c> if none is found or if the reflection hooks are unavailable.
        /// </summary>
        private static PropertyDrawer FindTypeDrawer(Type concreteType)
        {
            if (concreteType == null) return null;
            if (TypeDrawerCache.TryGetValue(concreteType, out var cached)) return cached;

            // Guard: if Unity ever renames the private fields, degrade gracefully.
            if (s_CpdType == null || s_CpdUseForChildren == null)
            {
                TypeDrawerCache[concreteType] = null;
                return null;
            }

            // Walk the inheritance chain so a drawer registered for an abstract base
            // with useForChildren:true is found even when concreteType is a leaf.
            for (Type target = concreteType; target != null && target != typeof(object); target = target.BaseType)
            {
                foreach (var drawerType in TypeCache.GetTypesWithAttribute<CustomPropertyDrawer>())
                {
                    if (!typeof(PropertyDrawer).IsAssignableFrom(drawerType)) continue;

                    foreach (var attr in drawerType.GetCustomAttributes<CustomPropertyDrawer>())
                    {
                        var attrTargetType = s_CpdType.GetValue(attr) as Type;
                        bool useForChildren = (bool)(s_CpdUseForChildren.GetValue(attr) ?? false);

                        if (attrTargetType == null) continue;

                        bool exactMatch = attrTargetType == target;
                        bool childrenMatch = useForChildren && attrTargetType.IsAssignableFrom(concreteType);

                        if (exactMatch || childrenMatch)
                        {
                            var drawer = (PropertyDrawer)Activator.CreateInstance(drawerType);
                            TypeDrawerCache[concreteType] = drawer;
                            return drawer;
                        }
                    }
                }
            }

            TypeDrawerCache[concreteType] = null;
            return null;
        }

        // ── Reflection helpers ─────────────────────────────────────────────────────────

        private static List<Type> GetSubclasses(Type baseType)
        {
            if (SubclassCache.TryGetValue(baseType, out var cached)) return cached;

            var result = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(SafeGetTypes)
                .Where(t => !t.IsAbstract && !t.IsGenericType && baseType.IsAssignableFrom(t) && t != baseType)
                .OrderBy(t => t.Namespace)
                .ThenBy(t => t.Name)
                .ToList();

            SubclassCache[baseType] = result;
            return result;
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static Type GetManagedReferenceBaseType(SerializedProperty property)
        {
            var parts = property.managedReferenceFieldTypename.Split(' ');
            var typeName = parts.Length == 2 ? parts[1] : parts[0];
            var assembly = parts.Length == 2 ? parts[0] : null;

            if (assembly != null)
            {
                var type = Type.GetType($"{typeName}, {assembly}");
                if (type != null) return type;
            }

            return AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(t => t.FullName == typeName);
        }

        // ── Iteration helpers ──────────────────────────────────────────────────────────

        private static IEnumerable<SerializedProperty> IterateVisibleChildren(SerializedProperty parent)
        {
            var current = parent.Copy();
            var end = parent.GetEndProperty();

            if (!current.NextVisible(true)) yield break;

            while (!SerializedProperty.EqualContents(current, end))
            {
                yield return current.Copy();
                if (!current.NextVisible(false)) break;
            }
        }

        // ── Display helpers ────────────────────────────────────────────────────────────

        private static string NiceName(Type type)
        {
            if (type == null) return "None";
            var name = type.Name.Replace("Move", " Move").Trim();
            return System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z])([A-Z])", " $1");
        }

        private static string GetMenuPath(Type type)
        {
            var ns = type.Namespace ?? "Global";
            return $"{ns}/{NiceName(type)}";
        }
    }
}