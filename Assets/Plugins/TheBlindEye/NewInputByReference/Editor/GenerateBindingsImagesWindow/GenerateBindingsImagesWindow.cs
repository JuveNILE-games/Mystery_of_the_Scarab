using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace NewInputByReference.EditorExtensions
{
    public class GenerateBindingsImagesWindow : EditorWindow
    {
        [SerializeField] private BindingsImages importedBindingsImages;
        [SerializeField] private List<Key> bindingsImagesList = new List<Key>();

        private bool _isImport;
        private string _folderPath;

        private int _currentLayoutIndex;
        private readonly Layout[] _layouts =
        {
            new KeyboardLayout(), new NumpadLayout(), new MouseLayout(), new GamepadLayout()
        };
        
        private SerializedObject _serializedObject;
        private SerializedProperty _importedBindingsImagesProperty;
        private SerializedProperty _bindingsImagesListProperty;
        
        private string FolderPath => "Assets" + (string.IsNullOrEmpty(_folderPath) ? null : '/' + _folderPath);
        
        private void OnEnable()
        {
            _serializedObject = new SerializedObject(this);
            _importedBindingsImagesProperty = _serializedObject.FindProperty("importedBindingsImages");
            _bindingsImagesListProperty = _serializedObject.FindProperty("bindingsImagesList");

            int startingIndex = 0;
            foreach (var layout in _layouts)
            {
                layout.OnEnable(startingIndex, bindingsImagesList, _bindingsImagesListProperty);
                startingIndex += layout.ListLength;
            }
            
            _serializedObject.Update();
        }

        [MenuItem("Tools/New Input By Reference/Generate Bindings Images")]
        private static void ShowWindow()
        {
            GetWindowWithRect<GenerateBindingsImagesWindow>(new Rect(0, 0, 1500, 625), false, "Generate Bindings Images");
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(0, 0, 1500, 625));

            DrawSettings();

            ChangeLabelWidth(1f);
            GUILayout.Space(Layout.LINE_SPACE);
            
            _layouts[_currentLayoutIndex].DrawLayout();
            
            GUILayout.EndArea();
            
            _serializedObject.ApplyModifiedProperties();
            _serializedObject.Update();
        }

        private void DrawSettings()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (!_isImport)
                    {
                        GUILayout.Space(105);
                        GUILayout.Label(new GUIContent("Current Folder Path: " + FolderPath, "The location where the BindingsImages Scriptable Object will be generated"));
                    }
                }
                
                if(_isImport)
                    GUILayout.Space(18);
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_isImport)
                    DrawImportSettings();
                else
                    DrawGenerateSettings();

                if (GUILayout.Button(new GUIContent("Layout", "Change the current layout")))
                    ChangeLayout();
                
                ChangeLabelWidth(40f);
                _isImport = EditorGUILayout.Toggle(new GUIContent ("Import", "Import a generated BindingsImages Scriptable Object"),
                    _isImport);
            }
            
            void DrawGenerateSettings()
            {
                ChangeLabelWidth(70f);
                _folderPath = EditorGUILayout.TextField(new GUIContent("Folder Path", "Provide a Folder Path. Default Folder Path is Assets/"),
                    _folderPath, GUILayout.Width(294));

                if (GUILayout.Button(new GUIContent("Generate", "Generate the BindingsImages Scriptable Object to the Current Folder Path")))
                    GenerateBindings();

                GUILayout.Space(596);
            }
        
            void DrawImportSettings()
            {
                ChangeLabelWidth(99f);
                EditorGUILayout.PropertyField(_importedBindingsImagesProperty, new GUIContent("Bindings Images"));
            
                if (GUILayout.Button(new GUIContent("Import", "Import the selected BindingsImages Scriptable Object")))
                    ImportBindings();
            
                if (GUILayout.Button(new GUIContent("Save", "Save the changes made to the selected BindingsImages Scriptable Object")))
                    SaveBindings();
            
                GUILayout.Space(430);
            }
        }

        private void ChangeLayout()
        {
            if (_currentLayoutIndex == _layouts.Length - 1)
            {
                _currentLayoutIndex = 0;
                return;
            }
            
            _currentLayoutIndex++;
        }

        private void GenerateBindings()
        {
            var bindingsImagesInstance = CreateInstance<BindingsImages>();
            string inputDataPath = CreateAssetPath("New BindingsImages");

            EditorUtility.SetDirty(bindingsImagesInstance);
            
            foreach(var layout in _layouts)
                layout.GenerateBindings(bindingsImagesInstance);
            
            AssetDatabase.CreateAsset(bindingsImagesInstance, inputDataPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            string CreateAssetPath(string assetName)
            {
                string assetPath = FolderPath + '/' + assetName + ".asset";
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

                return assetPath;
            }
        }
        
        private void SaveBindings()
        {
            if (!importedBindingsImages)
                return;
            
            foreach(var layout in _layouts)
                layout.SaveBindings(importedBindingsImages);
        }

        private void ImportBindings()
        {
            if (!importedBindingsImages)
                return;

            foreach(var layout in _layouts)
                layout.ImportBindings(importedBindingsImages);

            _serializedObject.ApplyModifiedProperties();
        }

        private void ChangeLabelWidth(float width) => EditorGUIUtility.labelWidth = width;
    }
}