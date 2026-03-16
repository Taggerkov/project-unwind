using Systems.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;


namespace Editor.ScriptableAnimationEditor
{
    [CustomPropertyDrawer(typeof(CinematicCameraSequence))]
    public class CinematicCameraSequenceDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            
            var field = new PropertyField(property, property.displayName);
            
            field.SetEnabled(false);

            container.Add(field);
            return container;
        }
    }
}
