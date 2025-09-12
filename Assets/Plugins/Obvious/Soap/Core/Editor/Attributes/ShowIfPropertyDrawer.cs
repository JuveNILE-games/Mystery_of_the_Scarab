using Obvious.Soap.Editor;
using UnityEditor;
using UnityEngine;

namespace Obvious.Soap.Attributes
{
    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    public class ShowIfPropertyDrawer : PropertyDrawer
    {
        private bool _showField = true;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attribute = (ShowIfAttribute) this.attribute;
            var conditionField = property.serializedObject.FindProperty(attribute.conditionFieldName);
            
            if (conditionField == null)
            {
                ShowError(position, label, "Field "+ attribute.conditionFieldName + " does not exist." );
                return;
            }

            _showField = ShouldShowField(conditionField, attribute);

            if (_showField)
            {
                EditorGUI.indentLevel++;
                var color = GUI.backgroundColor;
                GUI.backgroundColor = SoapEditorUtils.SoapColor;
                EditorGUI.PropertyField(position, property, label,true);
                EditorGUI.indentLevel--;
                GUI.backgroundColor = color;
            }
        }
        private bool ShouldShowField(SerializedProperty conditionField, ShowIfAttribute attribute)
        {
            try
            {
                switch (conditionField.propertyType)
                {
                    case SerializedPropertyType.Boolean:
                        bool comparisonBool = attribute.comparisonValue == null || (bool)attribute.comparisonValue;
                        return conditionField.boolValue == comparisonBool;
                    case SerializedPropertyType.Enum:
                        if (attribute.comparisonValue == null)
                        {
                            Debug.LogError("Comparison value is required for enum types.");
                            return false;
                        }
                        int enumValue = conditionField.enumValueIndex;
                        int comparisonEnumValue = (int)attribute.comparisonValue;
                        return enumValue == comparisonEnumValue;

                    default:
                        Debug.LogError($"Unsupported field type: {conditionField.propertyType}. Must be bool or enum.");
                        return false;
                }
            }
            catch
            {
                Debug.LogError("Invalid comparison value type.");
                return false;
            }
        }
        
        
        private void ShowError(Rect position, GUIContent label, string errorText)
        {
            EditorGUI.LabelField(position, label, new GUIContent(errorText));
            _showField = true;
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return _showField ? EditorGUI.GetPropertyHeight(property, label) : 0;
        }
    }
}