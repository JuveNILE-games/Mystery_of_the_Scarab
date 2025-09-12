using UnityEngine;
using UnityEditor;
using NewInputByReference.Bindings;

namespace NewInputByReference.EditorExtensions
{
    internal class MouseLayout : Layout
    {
        public MouseLayout()
        {
            var keysList = new[]
            {
                // 0 -> 2
                "LMB", "MMB", "RMB",
                
                // 3
                "Forward",
                
                // 4
                "Back"
            };
            
            Initialize(keysList, KeyType.Mouse);
        }

        public override void DrawLayout()
        {
            // LMB-> RMB 
            DrawKeyLine(0, 2, 300);
            
            // Forward
            DrawKeyLine(3, 3, 700);
            
            // Back
            DrawKeyLine(4, 4, 700);

            GUILayout.Space(100);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(300);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(new GUIContent("Legend:"));
                    GUILayout.Label(new GUIContent("LMB = Left Mouse Button"));
                    GUILayout.Label(new GUIContent("MMB = Middle Mouse Button"));
                    GUILayout.Label(new GUIContent("RMB = Right Mouse Button"));
                }
            }
        }
    }
}