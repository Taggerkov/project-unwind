#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Systems.Combat.Combatant.Behaviour;
using UnityEditor;
using UnityEngine;

namespace Systems.Combat.Combatant.Data.Editor
{
    /// <summary>
    /// Shared helpers used by StatsConstrainedMovesDrawer and StatsConstrainedSelectorDrawer.
    /// Not a drawer itself — no [CustomPropertyDrawer] attribute.
    /// </summary>
    internal static class CombatantMoveEditorUtils
    {
        // ── Subclass cache (keyed by base type, built once per session) ────────────────
        private static readonly Dictionary<Type, List<Type>> SubclassCache = new();

        // ── Stats-type resolution ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns the runtime type of the <c>StatsTemplate</c> field on the same
        /// serialized object as <paramref name="property"/>, or null if unassigned.
        /// Works for both list-element properties and standalone fields because both
        /// live on the same <see cref="SerializedObject"/> root.
        /// </summary>
        internal static Type GetStatsType(SerializedProperty property)
        {
            var statsProperty = property.serializedObject.FindProperty("StatsTemplate");
            return statsProperty?.managedReferenceValue?.GetType();
        }

        // ── Compatible-type enumeration ────────────────────────────────────────────────

        /// <summary>
        /// All non-abstract, non-open-generic <see cref="CombatantMove"/> subclasses
        /// that are valid for <paramref name="statsType"/>:
        /// <list type="bullet">
        ///   <item>statsType is null → return everything (no constraint possible).</item>
        ///   <item>Non-generic move (no TStats) → always included.</item>
        ///   <item>CombatantMove&lt;TStats&gt; → only when TStats == statsType.</item>
        /// </list>
        /// Results are ordered by namespace then name.
        /// </summary>
        internal static IEnumerable<Type> GetCompatibleMoveTypes(Type statsType)
        {
            return GetSubclasses(typeof(CombatantMove))
                .Where(t =>
                {
                    if (statsType == null) return true;
                    var arg = GetGenericStatsArgument(t);
                    return arg == null || arg == statsType;
                });
        }

        /// <summary>
        /// Walks <paramref name="moveType"/>'s inheritance chain looking for a closed
        /// <c>CombatantMove&lt;TStats&gt;</c> and returns its TStats type argument,
        /// or null for non-generic (common) moves.
        /// </summary>
        internal static Type GetGenericStatsArgument(Type moveType)
        {
            for (var t = moveType; t != null && t != typeof(object); t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(CombatantMove<>))
                    return t.GetGenericArguments()[0];
            }

            return null;
        }

        // ── Type menu ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Shows a context GenericMenu for changing the managed-reference type of the
        /// property at <paramref name="path"/> inside <paramref name="so"/>.
        /// </summary>
        internal static void ShowTypeMenu(
            Type currentType, Type statsType, SerializedObject so, string path)
        {
            var menu = new GenericMenu();

            // "None" entry
            menu.AddItem(new GUIContent("None"), currentType == null, () => { ApplyType(so, path, null); });
            menu.AddSeparator("");

            var types = GetCompatibleMoveTypes(statsType).ToList();

            if (statsType != null && types.Count == 0)
            {
                menu.AddDisabledItem(
                    new GUIContent($"No CombatantMove<{statsType.Name}> types found in project"));
            }

            foreach (var type in types)
            {
                var captured = type;
                menu.AddItem(
                    new GUIContent(GetMenuPath(type)),
                    currentType == type,
                    () => ApplyType(so, path, captured));
            }

            menu.ShowAsContext();
        }

        private static void ApplyType(SerializedObject so, string path, Type type)
        {
            var prop = so.FindProperty(path);
            if (prop == null) return;
            prop.managedReferenceValue = type != null ? Activator.CreateInstance(type) : null;
            prop.isExpanded = type != null;
            so.ApplyModifiedProperties();
        }

        // ── Display helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Human-readable name for a move type: strips the "Move" suffix and inserts
        /// spaces before capitals, e.g. <c>StandingJabMove</c> → <c>Standing Jab</c>.
        /// </summary>
        internal static string NiceName(Type type)
        {
            if (type == null) return "None";
            var name = Regex.Replace(type.Name, "(?<=[a-z])([A-Z])", " $1");
            return name.Replace("Move", "").Trim();
        }

        /// <summary>Groups the type in a GenericMenu by namespace for readability.</summary>
        internal static string GetMenuPath(Type type)
            => $"{type.Namespace ?? "Global"}/{NiceName(type)}";

        // ── Subclass cache ─────────────────────────────────────────────────────────────

        private static List<Type> GetSubclasses(Type baseType)
        {
            if (SubclassCache.TryGetValue(baseType, out var cached)) return cached;

            var result = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch
                    {
                        return Array.Empty<Type>();
                    }
                })
                .Where(t => !t.IsAbstract && !t.IsGenericType
                                          && baseType.IsAssignableFrom(t) && t != baseType)
                .OrderBy(t => t.Namespace)
                .ThenBy(t => t.Name)
                .ToList();

            SubclassCache[baseType] = result;
            return result;
        }
    }
}
#endif