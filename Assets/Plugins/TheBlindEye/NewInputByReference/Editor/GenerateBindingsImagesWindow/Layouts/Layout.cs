using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NewInputByReference.Bindings;

namespace NewInputByReference.EditorExtensions
{
    internal abstract class Layout
    {
        private const int NAME_SPACE = 26;
        private const int PREVIEW_SPACE = 10;
        private const int PREVIEW_SIZE = 40;
        private const string PROPERTY_NAME = "sprite";
        public const int LINE_SPACE = 15;
        
        private readonly List<Key> _bindingsImagesList = new List<Key>();
        
        private string[] _keysList;
        private KeyType _keyType;

        private int _startingBindingsIndex;
        
        private SerializedProperty _bindingsImagesListProperty;

        public int ListLength => _keysList.Length;

        public abstract void DrawLayout();

        public void OnEnable(int startingListIndex, List<Key> list, SerializedProperty listProperty)
        {
            _startingBindingsIndex = startingListIndex;
            _bindingsImagesListProperty = listProperty;
            
            foreach (var sprite in _bindingsImagesList)
                list.Add(sprite);
        }
        
        public void GenerateBindings(BindingsImages bindingsImagesInstance)
        {
            for (int i = 0; i < ListLength; i++)
            {
                string keyName = _keysList[i];
                bindingsImagesInstance.AddBinding(new Binding(keyName, _bindingsImagesList[i].sprite), _keyType);    
            }
        }
        
        public void ImportBindings(BindingsImages importedBindingsImages)
        {
            for (int i = 0; i < ListLength; i++)
            {
                _bindingsImagesList[i].sprite = importedBindingsImages.GetBindingSprite(i, _keyType);
            }
        }
        
        public void SaveBindings(BindingsImages importedBindingsImages)
        {
            for (int i = 0; i < ListLength; i++)
            {
                importedBindingsImages.SetSprite(_bindingsImagesList[i].sprite, i, _keyType);
            }
        }

        protected void Initialize(string[] keysList, KeyType keyType)
        {
            _keysList = keysList;
            _keyType = keyType;

            for (int i = 0; i < ListLength; i++)
            {
                _bindingsImagesList.Add(new Key());
            }
        }
        
        protected void DrawKeyLine(int startingIndex, int endingIndex, int lineSpace, int[] additionalSpace = null)
        {
            additionalSpace ??= new int[20];

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(lineSpace);
                for (int i = startingIndex, j = 0; i <= endingIndex; i++, j++)
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        CreateName(i, j);
                        CreatePreview(i, j);
                        CreateProperty(i, j);
                    }
                }
            }
            
            GUILayout.Space(LINE_SPACE);

            void CreateName(int keyIndex, int spaceIndex)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string key = _keysList[keyIndex];
                    
                    GUILayout.Space(NAME_SPACE - key.Length*3 + additionalSpace[spaceIndex]);
                    GUILayout.Label(key);
                }
            }

            void CreatePreview(int keyIndex, int spaceIndex)
            {
                using (new EditorGUILayout.HorizontalScope())
                { 
                    GUILayout.Space(PREVIEW_SPACE + additionalSpace[spaceIndex]);
                            
                    var sprite = _bindingsImagesList[keyIndex].sprite;
                    var texture = AssetPreview.GetAssetPreview(sprite);
                    
                    GUILayout.Label(texture, GUIStyle.none, GUILayout.Height(PREVIEW_SIZE), GUILayout.Width(PREVIEW_SIZE));
                }
            }

            void CreateProperty(int keyIndex, int spaceIndex)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(additionalSpace[spaceIndex]);

                    int propertyIndex = GetBindingsIndex(keyIndex);
                    var keyProperty = _bindingsImagesListProperty.GetArrayElementAtIndex(propertyIndex).FindPropertyRelative(PROPERTY_NAME);
                    
                    EditorGUILayout.PropertyField(keyProperty, GUIContent.none, GUILayout.ExpandWidth(false));
                }
            }
        }

        private int GetBindingsIndex(int index) => index + _startingBindingsIndex;
    }
}