#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Systems.Combat.Combatant.Behaviour;
using UnityEditor;
using UnityEngine;

namespace Systems.Combat.Combatant.Data.Editor
{
    /// <summary>
    /// Draws every [SerializeReference] CombatantMove field as a collapsible header row
    /// followed by its child fields when expanded.
    ///
    ///   ▶  Standing Jab   [Attack | Standing | No Hit/Block | Neutral]   (collapsed)
    ///   ▼  Standing Jab   [Attack | Standing | No Hit/Block | Neutral]   (expanded)
    ///        Damage   10
    ///        …
    ///
    /// This drawer is PURELY VISUAL. It does not own a type-selector button or menu —
    /// that responsibility belongs to the parent drawer:
    ///   • StatsConstrainedMovesDrawer   for List&lt;CombatantMove&gt; fields
    ///   • StatsConstrainedSelectorDrawer for standalone CombatantMove fields
    ///
    /// A right margin of ButtonMargin px is always left empty so that the parent's
    /// overlaid ▾ button lands cleanly without covering the rightmost badge.
    /// </summary>
    [CustomPropertyDrawer(typeof(CombatantMove), useForChildren: true)]
    public sealed class CombatantMoveDrawer : PropertyDrawer
    {
        /// <summary>Pixel height of the collapsed header row.</summary>
        internal const float HeaderHeight = 22f;

        /// <summary>Vertical padding in pixels inserted between the header and child fields, and between child fields.</summary>
        private const float Spacing = 2f;

        // ── PropertyDrawer overrides ───────────────────────────────────────────────────

        /// <summary>Renders the header row and, when expanded, all visible child properties of the managed reference.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, includeChildren: true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            DrawHeader(new Rect(position.x, position.y, position.width, HeaderHeight), property);

            if (property.isExpanded && property.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                float y = position.y + HeaderHeight + Spacing;

                foreach (var child in IterateVisibleChildren(property))
                {
                    float h = EditorGUI.GetPropertyHeight(child, includeChildren: true);
                    EditorGUI.PropertyField(
                        new Rect(position.x, y, position.width, h), child, includeChildren: true);
                    y += h + Spacing;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        /// <summary>Returns the total pixel height: header plus child fields when expanded.</summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
                return EditorGUI.GetPropertyHeight(property, label, includeChildren: true);

            float height = HeaderHeight;

            if (property.isExpanded && property.managedReferenceValue != null)
            {
                height += Spacing;
                foreach (var child in IterateVisibleChildren(property))
                    height += EditorGUI.GetPropertyHeight(child, includeChildren: true) + Spacing;
            }

            return height;
        }

        // ── Header drawing ─────────────────────────────────────────────────────────────

        /// <summary>Draws the tinted background, foldout arrow, move name, and badge strip for a single move header row.</summary>
        private static void DrawHeader(Rect rect, SerializedProperty property)
        {
            var currentType = property.managedReferenceValue?.GetType();

            // Background tint — same whether or not a parent button will overlay it
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                ? new Color(0.25f, 0.25f, 0.25f, 1f)
                : new Color(0.82f, 0.82f, 0.82f, 1f));

            // Foldout — 65 % of width; the rest is for badges + parent's button margin
            string moveName = currentType != null ? NiceName(currentType) : "— None —";
            property.isExpanded = EditorGUI.Foldout(
                new Rect(rect.x, rect.y + 2, rect.width * 0.65f, rect.height),
                property.isExpanded,
                moveName,
                toggleOnLabelClick: true);

            if (currentType != null)
                DrawBadges(rect, property);
        }

        /// <summary>Draws the coloured badge strip (commit type, hit/block conditions, character state, move type) right-aligned within <paramref name="rect"/>.</summary>
        private static void DrawBadges(Rect rect, SerializedProperty property)
        {
            var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };

            // Drawn right-to-left inside the reserved badge area.
            // On-screen order (left → right): Type | CharState | Guard | Commit
            (string text, Color color)[] badges =
            {
                (GetCommitTypeBadge(property), new Color(0.80f, 0.80f, 0.40f)), // yellow
                (GetHitBlockConditions(property), new Color(0.50f, 0.50f, 0.80f)), // blue
                (GetCharacterStateBadge(property), new Color(0.80f, 0.50f, 0.50f)), // red
                (GetTypeBadge(property), new Color(0.50f, 0.80f, 0.50f)), // green
            };

            float x = rect.xMax;

            foreach (var (text, color) in badges)
            {
                if (string.IsNullOrEmpty(text)) continue;
                style.normal.textColor = color;
                float w = style.CalcSize(new GUIContent(text)).x;
                x -= w;
                EditorGUI.LabelField(new Rect(x, rect.y, w, rect.height), text, style);
                x -= 6f;
                EditorGUI.DrawRect(new Rect(x + 2f, rect.y + 4, 1, rect.height - 8), Color.gray);
            }
        }

        // ── Badge value helpers ────────────────────────────────────────────────────────

        /// <summary>Returns the <see cref="EMoveType"/> badge string for the given move property.</summary>
        private static string GetTypeBadge(SerializedProperty p)
            => ReadEnumName(p, "type", "_type");

        /// <summary>Returns the character-state badge string for the given move property.</summary>
        private static string GetCharacterStateBadge(SerializedProperty p)
            => ReadEnumName(p, "characterState", "_characterState");

        /// <summary>Returns the commit-type badge string for the given move property.</summary>
        private static string GetCommitTypeBadge(SerializedProperty p)
            => ReadEnumName(p, "commitType", "_commitType");

        /// <summary>Returns a short human-readable label for the hit/block-conditions enum, or null when not found.</summary>
        private static string GetHitBlockConditions(SerializedProperty moveProp)
        {
            var prop = moveProp.FindPropertyRelative("hitBlockConditions")
                       ?? moveProp.FindPropertyRelative("_hitBlockConditions");
            if (prop == null || prop.propertyType != SerializedPropertyType.Enum) return null;

            var fi = GetFieldInHierarchy(moveProp.managedReferenceValue?.GetType(), prop.name);
            if (fi == null || !fi.FieldType.IsEnum) return null;

            return (EHitBlockConditions)Enum.ToObject(fi.FieldType, prop.intValue) switch
            {
                EHitBlockConditions.NotHitOrBlockstun => "No Hit/Block",
                EHitBlockConditions.HitOrBlockstunOk => "Hit/Block OK",
                EHitBlockConditions.HitOrBlockstunOnly => "Hit/Block Only",
                EHitBlockConditions.HitstunOnly => "Hitstun Only",
                EHitBlockConditions.BlockstunOnly => "Blockstun Only",
                _ => null
            };
        }

        /// <summary>Reads an enum field by <paramref name="name"/> or <paramref name="alt"/> from the managed reference and returns its name string, or null on failure.</summary>
        private static string ReadEnumName(SerializedProperty moveProp, string name, string alt)
        {
            var prop = moveProp.FindPropertyRelative(name)
                       ?? moveProp.FindPropertyRelative(alt);
            if (prop == null || prop.propertyType != SerializedPropertyType.Enum) return null;

            var fi = GetFieldInHierarchy(moveProp.managedReferenceValue?.GetType(), prop.name);
            if (fi == null || !fi.FieldType.IsEnum) return null;

            return Enum.GetName(fi.FieldType, prop.intValue);
        }

        // ── Reflection / iteration helpers ────────────────────────────────────────────

        /// <summary>Walks the type hierarchy from <paramref name="type"/> upward, returning the first <see cref="FieldInfo"/> matching <paramref name="fieldName"/>.</summary>
        private static FieldInfo GetFieldInHierarchy(Type type, string fieldName)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var fi = t.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null) return fi;
            }

            return null;
        }

        /// <summary>Yields each visible direct child property of <paramref name="parent"/> without recursing into grandchildren.</summary>
        private static IEnumerable<SerializedProperty> IterateVisibleChildren(SerializedProperty parent)
        {
            var current = parent.Copy();
            var end = parent.GetEndProperty();
            if (!current.NextVisible(enterChildren: true)) yield break;
            while (!SerializedProperty.EqualContents(current, end))
            {
                yield return current.Copy();
                if (!current.NextVisible(enterChildren: false)) break;
            }
        }

        /// <summary>Converts a PascalCase type name into a space-separated label, stripping the trailing "Move" suffix.</summary>
        private static string NiceName(Type type)
        {
            var name = Regex.Replace(type.Name, "(?<=[a-z])([A-Z])", " $1");
            return name.Replace("Move", "").Trim();
        }
    }
}
#endif