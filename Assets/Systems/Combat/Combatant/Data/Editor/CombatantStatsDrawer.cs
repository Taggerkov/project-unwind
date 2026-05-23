using System;
using System.Collections.Generic;
using System.Reflection;
using Systems.Combat.Combatant.Behaviour;
using UnityEditor;
using UnityEngine;

namespace Systems.Combat.Combatant.Data.Editor
{
    [CustomPropertyDrawer(typeof(CombatantStats), useForChildren: true)]
    public sealed class CombatantStatsDrawer : PropertyDrawer
    {
        // ── Constants ─────────────────────────────────────────────────────────────────────

        private static readonly Type s_BaseType = typeof(CombatantStats);

        private const float HeaderHeight = 20f;
        private const float HeaderSpacing = 4f;
        private const float SectionSpacing = 6f;

        private static readonly Color s_BaseHeaderColour = new(0.20f, 0.35f, 0.55f, 0.80f);
        private static readonly Color s_DerivedHeaderColour = new(0.30f, 0.50f, 0.30f, 0.80f);

        // Foldout state is keyed by SerializedProperty.propertyPath so each field on each
        // object gets its own toggle, surviving domain reloads (Dictionary resets, which is fine).
        private static readonly Dictionary<string, bool> s_BaseFoldouts = new();
        private static readonly Dictionary<string, bool> s_DerivedFoldouts = new();

        // ── PropertyDrawer overrides ──────────────────────────────────────────────────────

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.managedReferenceValue == null)
                return EditorGUIUtility.singleLineHeight;

            GetFieldGroups(property, out var baseProps, out var derivedProps);

            string key = property.propertyPath;
            bool baseOpen = GetFoldout(s_BaseFoldouts, key, defaultOpen: true);
            bool divOpen = GetFoldout(s_DerivedFoldouts, key, defaultOpen: true);

            float h = SectionHeight(baseProps, baseOpen);

            if (derivedProps.Count > 0)
                h += SectionSpacing + SectionHeight(derivedProps, divOpen);

            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.managedReferenceValue == null)
            {
                EditorGUI.LabelField(position, label, new GUIContent("(null reference)"));
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            GetFieldGroups(property, out var baseProps, out var derivedProps);

            string key = property.propertyPath;
            float y = position.y;

            // ── Base Stats ────────────────────────────────────────────────────────────────
            bool baseOpen = GetFoldout(s_BaseFoldouts, key, defaultOpen: true);
            baseOpen = DrawSection(ref y, position, "Base Stats", s_BaseHeaderColour, baseOpen, baseProps);
            s_BaseFoldouts[key] = baseOpen;

            // ── Derived Stats ─────────────────────────────────────────────────────────────
            if (derivedProps.Count > 0)
            {
                y += SectionSpacing;

                string typeName = ObjectNames.NicifyVariableName(property.managedReferenceValue.GetType().Name);
                bool derivedOpen = GetFoldout(s_DerivedFoldouts, key, defaultOpen: true);
                derivedOpen = DrawSection(ref y, position, typeName, s_DerivedHeaderColour, derivedOpen, derivedProps);
                s_DerivedFoldouts[key] = derivedOpen;
            }

            EditorGUI.EndProperty();
        }

        // ── Section drawing ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Draws a coloured foldout header followed by an indented block of properties.
        /// Advances <paramref name="y"/> by the total height consumed.
        /// Returns the (possibly toggled) foldout state.
        /// </summary>
        private static bool DrawSection(
            ref float y,
            Rect totalPos,
            string title,
            Color headerColour,
            bool open,
            List<SerializedProperty> props)
        {
            // — Header bar —
            var headerRect = new Rect(totalPos.x, y, totalPos.width, HeaderHeight);
            EditorGUI.DrawRect(headerRect, headerColour);

            // Foldout arrow + label rendered over the coloured bar
            var foldoutRect = new Rect(headerRect.x + 4f, headerRect.y, headerRect.width - 4f, headerRect.height);
            open = EditorGUI.Foldout(foldoutRect, open, title, toggleOnLabelClick: true, EditorStyles.foldoutHeader);
            y += HeaderHeight + HeaderSpacing;

            if (!open) return open;

            // — Property rows —
            EditorGUI.indentLevel++;
            foreach (var prop in props)
            {
                float propHeight = EditorGUI.GetPropertyHeight(prop, includeChildren: true);
                var propRect = new Rect(totalPos.x, y, totalPos.width, propHeight);
                EditorGUI.PropertyField(propRect, prop, includeChildren: true);
                y += propHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            EditorGUI.indentLevel--;

            return open;
        }

        // ── Height helpers ────────────────────────────────────────────────────────────────

        private static float SectionHeight(List<SerializedProperty> props, bool open)
        {
            // Header is always visible
            float h = HeaderHeight + HeaderSpacing;
            if (!open) return h;

            foreach (var p in props)
                h += EditorGUI.GetPropertyHeight(p, includeChildren: true) + EditorGUIUtility.standardVerticalSpacing;

            return h;
        }

        // ── Reflection helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Splits the serialized children of <paramref name="property"/> into two lists:
        /// fields declared directly on <c>CombatantStats</c>, and fields declared on any
        /// type further down the hierarchy (intermediate abstract layers included).
        /// </summary>
        private static void GetFieldGroups(
            SerializedProperty property,
            out List<SerializedProperty> baseProps,
            out List<SerializedProperty> derivedProps)
        {
            baseProps = new List<SerializedProperty>();
            derivedProps = new List<SerializedProperty>();

            if (property.managedReferenceValue == null) return;

            Type concreteType = property.managedReferenceValue.GetType();

            // Fields declared directly on the abstract base
            HashSet<string> baseNames = SerializedFieldNames(s_BaseType);

            // Fields declared between the base and the concrete type (exclusive lower bound,
            // inclusive upper bound) — covers multi-level inheritance chains.
            var derivedNames = new HashSet<string>();
            for (Type t = concreteType; t != null && t != s_BaseType; t = t.BaseType)
            {
                foreach (string name in SerializedFieldNames(t))
                    derivedNames.Add(name);
            }

            // Walk serialized children (depth = 1 only — we don't want grandchildren here)
            var iter = property.Copy();
            var end = property.GetEndProperty();

            if (!iter.NextVisible(enterChildren: true)) return;

            do
            {
                if (SerializedProperty.EqualContents(iter, end)) break;

                if (baseNames.Contains(iter.name))
                    baseProps.Add(iter.Copy());
                else if (derivedNames.Contains(iter.name))
                    derivedProps.Add(iter.Copy());
                // Fields not found in either set (e.g. hidden Unity internals) are silently skipped.
            } while (iter.NextVisible(enterChildren: false));
        }

        /// <summary>
        /// Returns the serialized field names declared *directly* on <paramref name="type"/>
        /// (i.e. <c>BindingFlags.DeclaredOnly</c>), respecting Unity's serialization rules.
        /// </summary>
        private static HashSet<string> SerializedFieldNames(Type type)
        {
            var result = new HashSet<string>();

            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            foreach (FieldInfo fi in type.GetFields(flags))
            {
                // Unity never serializes these
                if (fi.IsStatic || fi.IsLiteral || fi.IsInitOnly) continue;
                if (fi.IsDefined(typeof(NonSerializedAttribute), inherit: false)) continue;

                bool hasSerializeField = fi.IsDefined(typeof(SerializeField), inherit: false);
                bool hasSerializeRef = fi.IsDefined(typeof(SerializeReference), inherit: false);

                if (fi.IsPublic || hasSerializeField || hasSerializeRef)
                    result.Add(fi.Name);
            }

            return result;
        }

        // ── Foldout state helpers ─────────────────────────────────────────────────────────

        private static bool GetFoldout(Dictionary<string, bool> dict, string key, bool defaultOpen)
        {
            return dict.TryGetValue(key, out bool v) ? v : defaultOpen;
        }
    }
}