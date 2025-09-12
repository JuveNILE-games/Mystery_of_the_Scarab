using System;
using UnityEditor;
using UnityEngine;

namespace Obvious.Soap.Editor
{
    public class SoapWindowSettings
    {
        private FloatVariable _floatVariable;
        private readonly SerializedObject _exampleClassSerializedObject;
        private readonly SerializedProperty _currentHealthProperty;
        private readonly Texture[] _icons;
        private Vector2 _scrollPosition = Vector2.zero;
        private SoapSettings _settings;
        private float _defaultLabelWidth = EditorGUIUtility.labelWidth;
        
        public SoapWindowSettings()
        {
            var exampleClass = ScriptableObject.CreateInstance<ExampleClass>();
            _exampleClassSerializedObject = new SerializedObject(exampleClass);
            _currentHealthProperty = _exampleClassSerializedObject.FindProperty("CurrentHealth");
            _icons = new Texture[1];
            _icons[0] = EditorGUIUtility.IconContent("cs Script Icon").image;
        }

        public void Draw()
        {
            //Fixes weird bug with the label width
            EditorGUIUtility.labelWidth = _defaultLabelWidth;
            if (_settings == null)
                _settings = SoapEditorUtils.GetOrCreateSoapSettings();
            EditorGUILayout.BeginVertical();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawVariableDisplayMode();
            GUILayout.Space(10);
            if (_exampleClassSerializedObject != null) //can take a frame to initialize
            {
                DrawNamingModeOnCreation();
            }
            GUILayout.Space(10);
            DrawAllowRaisingEventsInEditor();
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }


        private void DrawVariableDisplayMode()
        {
#if ODIN_INSPECTOR
            GUI.enabled = false;
#endif
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _settings.VariableDisplayMode =
                (EVariableDisplayMode)EditorGUILayout.EnumPopup("Variable display mode",
                    _settings.VariableDisplayMode, GUILayout.Width(225));
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }

            var infoText = _settings.VariableDisplayMode == EVariableDisplayMode.Default
                ? "Displays all the parameters of variables."
                : "Only displays the value.";
            EditorGUILayout.LabelField(infoText, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();

#if !ODIN_INSPECTOR
            //Example
            // EditorGUILayout.BeginVertical(_skin.box);
            // if (_floatVariable == null)
            //     _floatVariable = ScriptableObject.CreateInstance<FloatVariable>();
            // var editor = UnityEditor.Editor.CreateEditor(_floatVariable);
            // editor.OnInspectorGUI();
            // EditorGUILayout.EndVertical();
#endif
#if ODIN_INSPECTOR
            GUI.enabled = true;
            EditorGUILayout.HelpBox("The variable display mode cannot be changed when using Odin Inspector.",
                MessageType.Warning);
#endif
        }

        private void DrawNamingModeOnCreation()
        {
            EditorGUILayout.BeginVertical();

            //Draw Naming Mode On Creation
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _settings.NamingOnCreationMode =
                (ENamingCreationMode)EditorGUILayout.EnumPopup("Naming mode on creation",
                    _settings.NamingOnCreationMode, GUILayout.Width(225));
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }

            var namingInfoText = _settings.NamingOnCreationMode == ENamingCreationMode.Auto
                ? "Automatically assigns a name on creation."
                : "Focus the created SO and let's you rename it.";
            EditorGUILayout.LabelField(namingInfoText, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();

            //Draw Create Path Mode
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _settings.CreatePathMode =
                (ECreatePathMode)EditorGUILayout.EnumPopup("Create Path Mode",
                    _settings.CreatePathMode, GUILayout.Width(225));

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }

            var pathInfoText = _settings.CreatePathMode == ECreatePathMode.Auto
                ? "Creates the asset in the selected path of the project window."
                : "Creates the asset at a custom path.";
            EditorGUILayout.LabelField(pathInfoText, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();

            //Draw Path
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 40;
            if (_settings.CreatePathMode == ECreatePathMode.Auto)
            {
                var guiStyle = new GUIStyle(EditorStyles.label);
                guiStyle.fontStyle = FontStyle.Italic;
                var path = SoapFileUtils.GetSelectedFolderPathInProjectWindow();
                EditorGUILayout.LabelField("Path:", $"{path}", guiStyle);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                var path = EditorGUILayout.TextField("Path:", SoapEditorUtils.CustomCreatePath);
                if (EditorGUI.EndChangeCheck())
                    SoapEditorUtils.CustomCreatePath = path;
            }

            EditorGUIUtility.labelWidth = labelWidth;

            EditorGUILayout.EndVertical();

            // //Example
            // {
            //     EditorGUILayout.BeginVertical(_skin.box);
            //     _exampleClassSerializedObject?.Update();
            //     EditorGUILayout.BeginHorizontal();
            //     var guiStyle = new GUIStyle(GUIStyle.none);
            //     guiStyle.contentOffset = new Vector2(0, 2);
            //     GUILayout.Box(_icons[0], guiStyle, GUILayout.Width(18), GUILayout.Height(18));
            //     GUILayout.Space(16);
            //     EditorGUILayout.LabelField("Example Class (Script)", EditorStyles.boldLabel);
            //     EditorGUILayout.EndHorizontal();
            //     GUILayout.Space(2);
            //     SoapInspectorUtils.DrawColoredLine(1, new Color(0f, 0f, 0f, 0.2f));
            //     EditorGUILayout.PropertyField(_currentHealthProperty);
            //     _exampleClassSerializedObject?.ApplyModifiedProperties();
            //     EditorGUILayout.EndVertical();
            // }
        }

        private void DrawAllowRaisingEventsInEditor()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
      
            _settings.RaiseEventsInEditor=(ERaiseEventInEditorMode)EditorGUILayout.EnumPopup("Raise events in editor",
                _settings.RaiseEventsInEditor, GUILayout.Width(225));
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }
            
            var pathInfoText = _settings.RaiseEventsInEditor == ERaiseEventInEditorMode.Disabled
                ? "Prevent raising events in editor mode."
                : "Allow raising events in editor mode.";
            EditorGUILayout.LabelField(pathInfoText, EditorStyles.wordWrappedMiniLabel);
            
            EditorGUILayout.EndHorizontal();
        }
    }

    [Serializable]
    public class ExampleClass : ScriptableObject
    {
        public FloatVariable CurrentHealth;
    }
}