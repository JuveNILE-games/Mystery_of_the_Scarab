using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;

namespace NewInputByReference.EditorExtensions
{
    public class GenerateInputDataWindow : EditorWindow
    {
        [SerializeField] private InputActionReference[] inputActionList;

        private ActionMap _generatedActionMap;
        private InputActionMap _actionMapCache;
        private string _actionMapPathCache;

        private bool _combineInputData;
        private bool _duplicateInputAction;
        private string _folderPath;
        
        private SerializedObject _serializedObject;
        private SerializedProperty _inputActionListProperty;

        private string FolderPath => "Assets" + (string.IsNullOrEmpty(_folderPath) ? null : '/' + _folderPath);
        
        private void OnEnable()
        {
            _serializedObject = new SerializedObject(this);
            _inputActionListProperty = _serializedObject.FindProperty("inputActionList");
        }

        [MenuItem("Tools/New Input By Reference/Generate Input Data")]
        public static void ShowWindow()
        {
            GetWindow<GenerateInputDataWindow>("Generate Input Data");
        }
        
        private void OnGUI()
        {
            GUILayout.Space(20f);

            EditorGUILayout.PropertyField(_inputActionListProperty, new GUIContent("Input Action List"));
            
            GUILayout.Space(20f);

            _folderPath = EditorGUILayout.TextField(new GUIContent("Folder Path", "Provide a Folder Path. Default Folder Path is Assets/"),
                          _folderPath);
            
            if(!_duplicateInputAction)
                _combineInputData = EditorGUILayout.Toggle(new GUIContent("Combine Input Data", "Combine generated Input Data into a single Action Map"),
                                    _combineInputData);
            
            if(!_combineInputData)  
                _duplicateInputAction = EditorGUILayout.Toggle(new GUIContent ("Duplicate Input Data", "Generate Input Data even if in the Current Folder Path is already a duplicate of it"),
                                        _duplicateInputAction);
            
            GUILayout.Space(10f);
            
            if (GUILayout.Button(new GUIContent("Generate Input Data", "Generate Input Data to the Current Folder Path")))
                GenerateInputData();
            
            if (GUILayout.Button(new GUIContent("Delete Input Data", "Delete Input Data from the Current Folder Path")))
                DeleteInputData();
            
            GUILayout.Space(10f);
            
            GUILayout.Label(new GUIContent("Current Folder Path: " + FolderPath, "The location where the Input Data will be generated/deleted"));
            
            _serializedObject.ApplyModifiedProperties();
        }

        private void GenerateInputData()
        {
            if (inputActionList == null)
                return;

            InputData[] inputDataList = null;
            
            if (!_duplicateInputAction)
                inputDataList = ExtractInputDataList<InputData>(FolderPath);

            foreach (var inputAction in inputActionList)
            {
                if (!inputAction)
                    continue;

                if (!_duplicateInputAction && !_combineInputData && IsInputDataCreated(inputDataList, inputAction))
                    continue; 

                var actionMap = inputAction.action.actionMap;
                if (_combineInputData && _actionMapCache != actionMap)
                {
                    _actionMapPathCache = CreateAssetPath(actionMap.name);
                    _actionMapCache = actionMap;
                    
                    CreateActionMap();
                }

                string inputDataPath = CreateAssetPath(inputAction.action.name);
                CreateInputData(inputAction, inputDataPath);
            }
            
            _actionMapCache = null;
            _actionMapPathCache = null;
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void DeleteInputData()
        {
            string[] inputDataPathsList = GetInputDataPathsList(FolderPath);

            foreach (string inputDataPath in inputDataPathsList)
                AssetDatabase.DeleteAsset(inputDataPath);
        }
        
        private string CreateAssetPath(string assetName)
        {
            string assetPath = FolderPath + '/' + assetName + ".asset";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            return assetPath;
        }
        
        private void CreateActionMap()
        {
            var actionMapInstance = CreateInstance<ActionMap>();
            AssetDatabase.CreateAsset(actionMapInstance, _actionMapPathCache);
             
            _generatedActionMap = AssetDatabase.LoadAssetAtPath<ActionMap>(_actionMapPathCache); 
        }

        private void CreateInputData(InputActionReference inputAction, string inputDataPath)
        {
            InputData inputDataInstance = inputAction.action.expectedControlType switch
            {
                "Button" => CreateInstance<ButtonInputData>(),
                "Axis" => CreateInstance<AxisInputData>(),
                "Vector2" => CreateInstance<Vector2InputData>(),
                "Vector3" => CreateInstance<Vector3InputData>(),
                _ => CreateInstance<AxisInputData>()
            };
            
            inputDataInstance.SetInputAction(inputAction);

            if (!_combineInputData)
            {
                AssetDatabase.CreateAsset(inputDataInstance, inputDataPath);
                return;
            }
            
            inputDataInstance.name = inputAction.action.name;
            
            //_generatedActionMap.AddInputData(inputDataInstance);
            AssetDatabase.AddObjectToAsset(inputDataInstance, _actionMapPathCache);
        }

        private static bool IsInputDataCreated(InputData[] inputDataList, InputActionReference inputAction)
        {
            foreach (var inputData in inputDataList)
            {
                if(!inputData)
                    continue;
                
                if (inputData.InputAction == inputAction.action)
                    return true;
            }
            
            return false;
        }
        
        private static T[] ExtractInputDataList<T>(string folderPath) where T : Object
        {
            string[] inputDataPathList = GetInputDataPathsList(folderPath);
            var inputDataList = new T[inputDataPathList.Length];
            
            for (int i = 0; i < inputDataList.Length; i++)
                inputDataList[i] = AssetDatabase.LoadAssetAtPath<T>(inputDataPathList[i]);

            return inputDataList;
        }
        
        private static string[] GetInputDataPathsList(string folderPath)
        {
            string[] inputDataPathList = AssetDatabase.FindAssets("t:InputData", new[] {folderPath});
            
            for (int i = 0; i < inputDataPathList.Length; i++)
                inputDataPathList[i] = AssetDatabase.GUIDToAssetPath(inputDataPathList[i]);

            return inputDataPathList;
        }
    }
}
