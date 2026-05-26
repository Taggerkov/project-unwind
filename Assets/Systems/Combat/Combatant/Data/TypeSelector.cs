using System;
using UnityEngine;

namespace Systems.Combat.Combatant.Data
{
    /// <summary>
    /// Apply to a [SerializeReference] field or list to get a type-selector dropdown in the inspector.
    /// The drawer will enumerate all non-abstract subclasses of the field's declared type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class TypeSelector : PropertyAttribute
    {
        /// <summary>When true, the drawer renders the selected type's child properties inline below the dropdown.</summary>
        public bool DrawChildren { get; }

        /// <summary>Creates a <see cref="TypeSelector"/> attribute.</summary>
        /// <param name="drawChildren">Whether to expand child properties inline. Defaults to true.</param>
        public TypeSelector(bool drawChildren = true)
        {
            DrawChildren = drawChildren;
        }
    }
}