using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

# if !ODIN_INSPECTOR
namespace Obvious.Soap.Editor
{
    [CustomEditor(typeof(IntVariable), true)]
    public class IntVariableDrawer : ScriptableVariableDrawer
    {
        private IntVariable _intVariable = null;

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();
            if (_intVariable == null)
                _intVariable = target as IntVariable;

            if (_soapSettings.VariableDisplayMode == EVariableDisplayMode.Minimal)
                DrawMinimal();
            else
                DrawDefault();

            if (serializedObject.ApplyModifiedProperties())
                EditorUtility.SetDirty(target);

            DrawPlayModeObjects();
        }
        
        protected override void DrawDefault(Type isGenericType = null)
        {
            serializedObject.DrawOnlyField("m_Script", true);
            var propertiesToHide = new HashSet<string>() { "m_Script", "_guid", "_saveGuid" };
            if (!_intVariable.IsClamped)
            {
                propertiesToHide.Add("_min");
                propertiesToHide.Add("_max");
            }
            serializedObject.DrawCustomInspector(propertiesToHide, null);

            if (GUILayout.Button("Reset Value"))
            {
                var so = (IReset)target;
                so.ResetValue();
            }
        }
    }
}
#endif
