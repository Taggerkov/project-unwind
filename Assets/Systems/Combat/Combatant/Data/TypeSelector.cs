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
        public bool DrawChildren { get; }

        public TypeSelector(bool drawChildren = true)
        {
            DrawChildren = drawChildren;
        }
    }
}