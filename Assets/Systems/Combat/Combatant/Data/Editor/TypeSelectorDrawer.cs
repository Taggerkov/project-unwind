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
                foreach (var child in IterateVisibleChildren(property))
                    height += EditorGUI.GetPropertyHeight(child, true) + Spacing;
            }

            return height;
        }

        // ── Dropdown drawing ───────────────────────────────────────────────────────────

        private void DrawTypeDropdown(Rect rect, SerializedProperty property, GUIContent label)
        {
            var baseType = GetManagedReferenceBaseType(property);
            var currentType = property.managedReferenceValue?.GetType();
            var buttonLabel = currentType != null ? NiceName(currentType) : $"<None>  ({NiceName(baseType)})";

            // Draw the label on the left, dropdown button on the right
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

            // "None" entry
            menu.AddItem(
                new GUIContent("None"),
                currentType == null,
                () => SetType(property, null));

            menu.AddSeparator("");

            // One entry per concrete subclass, grouped by namespace
            foreach (var type in subTypes)
            {
                var t = type; // capture
                var displayName = GetMenuPath(type);
                menu.AddItem(
                    new GUIContent(displayName),
                    currentType == type,
                    () => SetType(property, t));
            }

            menu.ShowAsContext();
        }

        private static void SetType(SerializedProperty property, Type type)
        {
            property.managedReferenceValue = type != null ? Activator.CreateInstance(type) : null;
            property.serializedObject.ApplyModifiedProperties();
        }

        // ── Child property drawing ─────────────────────────────────────────────────────

        private static void DrawChildren(Rect position, SerializedProperty property, ref float y)
        {
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

        // ── Reflection helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns all non-abstract subclasses of <paramref name="baseType"/>
        /// found across all loaded assemblies, cached after the first call.
        /// </summary>
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

        /// <summary>
        /// Parses Unity's "assemblyName typeName" format for managedReferenceFieldTypename.
        /// </summary>
        private static Type GetManagedReferenceBaseType(SerializedProperty property)
        {
            var parts = property.managedReferenceFieldTypename.Split(' ');
            var typeName = parts.Length == 2 ? parts[1] : parts[0];
            var assembly = parts.Length == 2 ? parts[0] : null;

            // Try direct lookup first
            if (assembly != null)
            {
                var type = Type.GetType($"{typeName}, {assembly}");
                if (type != null) return type;
            }

            // Fall back to searching all loaded assemblies
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

        /// <summary>Converts CamelCase type name to readable label, e.g. StandingPunchMove → Standing Punch Move.</summary>
        private static string NiceName(Type type)
        {
            if (type == null) return "None";
            var name = type.Name.Replace("Move", " Move").Trim();
            // Insert spaces before capitals
            return System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z])([A-Z])", " $1");
        }

        /// <summary>Groups type in the menu by namespace for readability.</summary>
        private static string GetMenuPath(Type type)
        {
            var ns = type.Namespace ?? "Global";
            return $"{ns}/{NiceName(type)}";
        }
    }
}